using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Application;
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

    //these are my 2 rules that can fail on update of the db
    //so i initialise them here to catch the errors when i make a request 
    private const string OneContributionPerCycleIndex = "IX_Contributions_StokvelId_MemberUserId_Cycle";
    private const string CycleLabelKey = "AK_ContributionCycles_StokvelId_Label";

    private readonly TallyVelDbContext _db;

    public EfContributionRepository(TallyVelDbContext db)
    {
        _db = db;
    }

    public IReadOnlyCollection<Contribution> GetAll() => _db.Contributions.OrderBy(c => c.RecordedAt).ToList().AsReadOnly();

    public Contribution? GetById(Guid id) => _db.Contributions.FirstOrDefault(c => c.Id == id);

    //this methos will check if the user has already contributed 
    
    public bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle)
    {
        //TODO: Capitalise
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

    public async Task<(IReadOnlyList<Contribution> Items, string NextPageToken)> GetPageAsync(
    contributionQuery q, CancellationToken ct = default)
{
    // 1. Filters — all become WHERE in SQL

    //This is the initial query that retrieve everything 
    // then get refined along the way
    var query = _db.Contributions
        .AsNoTracking()
        .Where(c => c.StokvelId == q.StokvelId);

    //refinining part through the conditions 
    if (q.Cycle is { } cycle)         query = query.Where(c => c.Cycle == cycle);
    if (q.MemberUserId is { } member) query = query.Where(c => c.MemberUserId == member);
    if (q.MinAmount is { } min)       query = query.Where(c => c.Amount >= min);
    if (q.MaxAmount is { } max)       query = query.Where(c => c.Amount <= max);

    // 2. Keyset: continue after the last row of the previous page
    if (!string.IsNullOrEmpty(q.PageToken))
    {
        var t = ContributionPageToken.Decode(q.PageToken);

        // Malformed, or reused with a different filter/sort → 400
        if (t is null || t.QueryFingerprint != q.Fingerprint())
            throw new InvalidQueryException("pageToken",
                "Invalid page token, or token used with a different filter or sort.");

         //At this point the query get tuned for the database 
        query = (q.SortBy, q.Descending) switch 
        {
            (ContributionSortField.RecordedAt, false) => query.Where(c =>
                EF.Functions.GreaterThan(
                    ValueTuple.Create(c.RecordedAt, c.Id),
                    ValueTuple.Create(t.LastRecordedAt!.Value, t.LastId))),

            (ContributionSortField.RecordedAt, true) => query.Where(c =>
                EF.Functions.LessThan(
                    ValueTuple.Create(c.RecordedAt, c.Id),
                    ValueTuple.Create(t.LastRecordedAt!.Value, t.LastId))),

            (ContributionSortField.Amount, false) => query.Where(c =>
                EF.Functions.GreaterThan(
                    ValueTuple.Create(c.Amount, c.Id),
                    ValueTuple.Create(t.LastAmount!.Value, t.LastId))),

            _ => query.Where(c =>
                EF.Functions.LessThan(
                    ValueTuple.Create(c.Amount, c.Id),
                    ValueTuple.Create(t.LastAmount!.Value, t.LastId))),
        };
    }

    // 3. Deterministic order: sort field, then Id as tiebreaker → ORDER BY in SQL
    query = (q.SortBy, q.Descending) switch
    {
        (ContributionSortField.RecordedAt, false) => query.OrderBy(c => c.RecordedAt).ThenBy(c => c.Id),
        (ContributionSortField.RecordedAt, true)  => query.OrderByDescending(c => c.RecordedAt).ThenByDescending(c => c.Id),
        (ContributionSortField.Amount, false)     => query.OrderBy(c => c.Amount).ThenBy(c => c.Id),
        _                                         => query.OrderByDescending(c => c.Amount).ThenByDescending(c => c.Id),
    };

    // 4. One extra row tells us if there's another page → LIMIT in SQL
    var rows = await query.Take(q.PageSize + 1).ToListAsync(ct);

    var hasMore = rows.Count > q.PageSize;
    if (hasMore) rows.RemoveAt(rows.Count - 1);

    // 5. Next token: empty exactly when there are no more results
    var next = string.Empty;
    if (hasMore)
    {
        var last = rows[^1];
        next = new ContributionPageToken(
            last.RecordedAt, last.Amount, last.Id, q.Fingerprint()).Encode();
    }

    return (rows, next);
}
}
