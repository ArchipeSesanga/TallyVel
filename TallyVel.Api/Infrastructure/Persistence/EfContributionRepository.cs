using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Stores Contributions in PostgreSQL. Same rationale as
/// <see cref="EfUserRepository"/>.
///
/// A Contribution's Cycle string is a foreign key to a ContributionCycle
/// row, but callers only know the string. So <see cref="AddAsync"/> finds
/// or creates that row itself.
/// </summary>
public sealed class EfContributionRepository : IContributionRepository
{
    private const string OneContributionPerCycleIndex = "IX_Contributions_StokvelId_MemberUserId_Cycle";
    private const string CycleLabelKey = "AK_ContributionCycles_StokvelId_Label";

    private readonly TallyVelDbContext _db;

    public EfContributionRepository(TallyVelDbContext db)
    {
        _db = db;
    }

    public IReadOnlyCollection<Contribution> GetAll() => _db.Contributions.OrderBy(c => c.RecordedAt).ToList().AsReadOnly();

    public Contribution? GetById(Guid id) => _db.Contributions.FirstOrDefault(c => c.Id == id);

    public bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle)
    {
        var label = cycle.Trim();
        return _db.Contributions.Any(c =>
            c.StokvelId == stokvelId && c.MemberUserId == memberUserId && c.Cycle == label);
    }

    public async Task AddAsync(Contribution contribution)
    {
        var cycleExists = await _db.ContributionCycles.AnyAsync(c =>
            c.StokvelId == contribution.StokvelId && c.Label == contribution.Cycle);

        ContributionCycle? newCycle = null;
        if (!cycleExists)
        {
            newCycle = new ContributionCycle(contribution.StokvelId, contribution.Cycle);
            _db.ContributionCycles.Add(newCycle);
        }

        _db.Contributions.Add(contribution);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (newCycle is not null && ex.IsUniqueViolation(CycleLabelKey))
        {
            // A concurrent request created the same cycle between our
            // check and our insert. Drop our copy and save the
            // contribution against theirs.
            _db.Entry(newCycle).State = EntityState.Detached;
            await SaveContributionAsync(contribution);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(OneContributionPerCycleIndex))
        {
            throw DuplicateContribution(contribution);
        }
    }

    public async Task UpdateAsync(Contribution contribution)
    {
        var existing = await _db.Contributions.FindAsync(contribution.Id)
            ?? throw new NotFoundException("contribution", contribution.Id);

        if (!ReferenceEquals(existing, contribution))
            _db.Entry(existing).CurrentValues.SetValues(contribution);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var existing = await _db.Contributions.FindAsync(id)
            ?? throw new NotFoundException("contribution", id);

        _db.Contributions.Remove(existing);
        await _db.SaveChangesAsync();
    }

    private async Task SaveContributionAsync(Contribution contribution)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(OneContributionPerCycleIndex))
        {
            throw DuplicateContribution(contribution);
        }
    }

    private AlreadyExistsException DuplicateContribution(Contribution contribution)
    {
        _db.Entry(contribution).State = EntityState.Detached;
        return new AlreadyExistsException(
            "contribution-already-recorded",
            $"User {contribution.MemberUserId} has already contributed to stokvel {contribution.StokvelId} for cycle '{contribution.Cycle}'.");
    }
}
