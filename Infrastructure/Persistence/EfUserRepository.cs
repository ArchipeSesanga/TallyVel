using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Domain;

namespace Infrastructure.Persistence;

/// <summary>
/// Stores Users in PostgreSQL through <see cref="TallyVelDbContext"/>.
/// Registered as Scoped, because a DbContext lives for one request —
/// unlike the in-memory version, which had to be a Singleton to keep
/// its data. The interface is synchronous, so this uses EF's
/// synchronous calls.
/// </summary>
public sealed class EfUserRepository : IUserRepository
{
    private readonly TallyVelDbContext _db;

    public EfUserRepository(TallyVelDbContext db)
    {
        _db = db;
    }

    public IReadOnlyCollection<User> GetAll() => _db.Users.OrderBy(u => u.CreatedAt).ToList().AsReadOnly();

    public User? GetById(Guid id) => _db.Users.FirstOrDefault(u => u.Id == id);

    /// <summary>
    /// Saves immediately, like the in-memory version, so the user exists
    /// as soon as this returns.
    /// </summary>
    public void Add(User user)
    {
        _db.Users.Add(user);

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_Users_Email"))
        {
            _db.Entry(user).State = EntityState.Detached;
            throw new AlreadyExistsException(
                "user-email-already-exists", "A user with this email already exists.");
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            _db.Entry(user).State = EntityState.Detached;
            throw new InvalidOperationException($"A user with id {user.Id} already exists.");
        }
    }

    public Task<bool> EmailExistsAsync(string normalisedEmail, CancellationToken ct) =>
        _db.Users.AnyAsync(u => u.Email == normalisedEmail, ct);

    public Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == normalisedEmail, ct);

    /// <summary>
    /// Saves immediately, like <see cref="Add"/>, and turns a duplicate
    /// email (including a race between two registrations) into
    /// <see cref="AlreadyExistsException"/>.
    /// </summary>
    public async Task AddAsync(User user, CancellationToken ct)
    {
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_Users_Email"))
        {
            _db.Entry(user).State = EntityState.Detached;
            throw new AlreadyExistsException(
                "user-email-already-exists", "A user with this email already exists.");
        }
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>Saves changes made to a User that came from this repository.</summary>
    public async Task UpdateAsync(User user, CancellationToken ct) =>
        await _db.SaveChangesAsync(ct);
}
