using System.Collections.Concurrent;
using Domain;

namespace Infrastructure.Persistence;

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

    public Task<bool> EmailExistsAsync(string normalisedEmail, CancellationToken ct) =>
        Task.FromResult(_users.Values.Any(u => u.Email == normalisedEmail));

    public Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == normalisedEmail));

    public Task AddAsync(User user, CancellationToken ct)
    {
        if (_users.Values.Any(u => u.Email == user.Email))
            throw new AlreadyExistsException(
                "user-email-already-exists", $"A user with email '{user.Email}' already exists.");

        Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(GetById(id));

    // Users are shared objects here, so changes are already stored.
    public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask;
}
