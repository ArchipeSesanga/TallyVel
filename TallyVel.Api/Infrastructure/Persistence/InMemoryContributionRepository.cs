using System.Collections.Concurrent;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Holds Contributions in memory instead of a database. Same rationale
/// as <see cref="InMemoryStokvelRepository"/>.
/// </summary>
public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentDictionary<Guid, Contribution> _contributions = new();

    public IReadOnlyCollection<Contribution> GetAll() => _contributions.Values.ToList().AsReadOnly();

    public Contribution? GetById(Guid id) => _contributions.GetValueOrDefault(id);

    public Task AddAsync(Contribution contribution)
    {
        if (!_contributions.TryAdd(contribution.Id, contribution))
            throw new InvalidOperationException($"A contribution with id {contribution.Id} already exists.");

        return Task.CompletedTask;
    }

    public bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle) =>
        _contributions.Values.Any(c =>
            c.StokvelId == stokvelId &&
            c.MemberUserId == memberUserId &&
            string.Equals(c.Cycle, cycle.Trim(), StringComparison.Ordinal));

    public Task UpdateAsync(Contribution contribution)
    {
        // addValueFactory only runs if the key is absent, so this fails
        // fast instead of silently inserting under the name "update".
        _contributions.AddOrUpdate(
            contribution.Id,
            addValueFactory: _ => throw new NotFoundException("contribution", contribution.Id),
            updateValueFactory: (_, _) => contribution);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<(IReadOnlyList<Contribution> Items, string NextPageToken)> GetPageAsync(contributionQuery q, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
