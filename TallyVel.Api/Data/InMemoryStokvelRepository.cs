using System.Collections.Concurrent;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// Holds Stokvels in memory instead of a database. Same rationale as
/// <see cref="InMemoryUserRepository"/>.
/// </summary>
public sealed class InMemoryStokvelRepository : IStokvelRepository
{
    private readonly ConcurrentDictionary<Guid, Stokvel> _stokvels = new();

    public InMemoryStokvelRepository(IEnumerable<Stokvel> seedStokvels)
    {
        foreach (var stokvel in seedStokvels)
            _stokvels[stokvel.Id] = stokvel;
    }

    public IReadOnlyCollection<Stokvel> GetAll() => _stokvels.Values.ToList().AsReadOnly();

    public Stokvel? GetById(Guid id) => _stokvels.GetValueOrDefault(id);

    public void Add(Stokvel stokvel)
    {
        if (!_stokvels.TryAdd(stokvel.Id, stokvel))
            throw new InvalidOperationException($"A stokvel with id {stokvel.Id} already exists.");
    }
}