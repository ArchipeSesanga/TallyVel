using Domain;

namespace Domain;

/// <summary>
/// An abstraction over where Users are stored. Controllers and services
/// depend on this interface, not on any specific storage technology —
/// so swapping the in-memory implementation for an EF Core one later
/// won't require touching a single controller.
/// </summary>
public interface IUserRepository
{
    IReadOnlyCollection<User> GetAll();
    User? GetById(Guid id);
    void Add(User user);

    Task<bool> EmailExistsAsync(string normalisedEmail, CancellationToken ct);
    Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct); // needed for login
    Task AddAsync(User user, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
}