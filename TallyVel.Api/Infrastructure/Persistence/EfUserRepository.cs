using Microsoft.EntityFrameworkCore;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

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
                "user-email-already-exists", $"A user with email '{user.Email}' already exists.");
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            _db.Entry(user).State = EntityState.Detached;
            throw new InvalidOperationException($"A user with id {user.Id} already exists.");
        }
    }
}
