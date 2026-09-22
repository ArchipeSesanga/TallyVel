using System.Collections.Concurrent;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Data;

/// <summary>
/// Holds Contributions in memory instead of a database. Same rationale
/// as <see cref="InMemoryStokvelRepository"/>.
/// </summary>
public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentDictionary<Guid, Contribution> _contributions = new();

    public IReadOnlyCollection<Contribution> GetAll() => _contributions.Values.ToList().AsReadOnly();

    public Contribution? GetById(Guid id) => _contributions.GetValueOrDefault(id);

    public void Add(Contribution contribution)
    {
        if (!_contributions.TryAdd(contribution.Id, contribution))
            throw new InvalidOperationException($"A contribution with id {contribution.Id} already exists.");
    }

    public bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle) =>
        _contributions.Values.Any(c =>
            c.StokvelId == stokvelId &&
            c.MemberUserId == memberUserId &&
            string.Equals(c.Cycle, cycle.Trim(), StringComparison.Ordinal));
}
