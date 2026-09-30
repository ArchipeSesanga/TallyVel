using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Stores Stokvels (and their memberships) in PostgreSQL. Same
/// rationale as <see cref="EfUserRepository"/>.
///
/// Stokvels come back tracked by the DbContext, with Members loaded. The
/// membership service changes a stokvel after fetching it and never calls
/// back into the repository, so those changes are saved by
/// <see cref="SaveChangesFilter"/> at the end of the request.
/// </summary>
public sealed class EfStokvelRepository : IStokvelRepository
{
    private readonly TallyVelDbContext _db;

    public EfStokvelRepository(TallyVelDbContext db)
    {
        _db = db;
    }

    // Explicit ordering: Postgres returns rows in no guaranteed order, and
    // the in-memory version's callers saw the creator first, then members
    // in the order they joined.
    public IReadOnlyCollection<Stokvel> GetAll() =>
        _db.Stokvels
            .Include(s => s.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId))
            .OrderBy(s => s.CreatedAt)
            .ToList()
            .AsReadOnly();

    public Stokvel? GetById(Guid id) =>
        _db.Stokvels
            .Include(s => s.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId))
            .FirstOrDefault(s => s.Id == id);

    public void Add(Stokvel stokvel)
    {
        _db.Stokvels.Add(stokvel);

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            _db.Entry(stokvel).State = EntityState.Detached;
            throw new InvalidOperationException($"A stokvel with id {stokvel.Id} already exists.");
        }
    }
}
