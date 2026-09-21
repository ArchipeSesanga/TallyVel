using System.Collections.Concurrent;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// Holds Users in memory instead of a database. Backed by a
/// ConcurrentDictionary since ASP.NET Core handles requests
/// concurrently, and this is registered as a Singleton in DI — a
/// Scoped or Transient registration would reset to just the seed data
/// on every request.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public InMemoryUserRepository(IEnumerable<User> seedUsers)
    {
        foreach (var user in seedUsers)
            _users[user.Id] = user;
    }

    public IReadOnlyCollection<User> GetAll() => _users.Values.ToList().AsReadOnly();

    public User? GetById(Guid id) => _users.GetValueOrDefault(id);

    public void Add(User user)
    {
        if (!_users.TryAdd(user.Id, user))
            throw new InvalidOperationException($"A user with id {user.Id} already exists.");
    }
}