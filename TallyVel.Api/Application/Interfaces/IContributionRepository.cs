using TallyVel.Api.Domain;

namespace TallyVel.Api.Application.Interfaces;

/// <summary>
/// An abstraction over where Contributions are stored. Same rationale
/// as <see cref="IStokvelRepository"/>. Pure persistence only — the
/// contribution-recording workflow (idempotency, existence checks,
/// business rules) lives in <see cref="IContributionService"/>.
/// </summary>
public interface IContributionRepository
{
    //create
    Task AddAsync(Contribution contribution);

    //Read
    IReadOnlyCollection<Contribution> GetAll();
    Contribution? GetById(Guid id);

    // Update
    Task UpdateAsync(Contribution contribution);

    // Delete
    Task DeleteAsync(Guid id);

    /// <summary>
    /// True if this member already has a contribution recorded for this
    /// exact stokvel + cycle. This is what makes "one contribution per
    /// member per cycle" an enforceable rule instead of just a
    /// convention — <see cref="IContributionService"/> relies on this to
    /// reject a second payment for a cycle that's already covered.
    /// </summary>
    bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle);

    Task<(IReadOnlyList<Contribution> Items, string NextPageToken)> GetPageAsync(
    contributionQuery q, CancellationToken ct = default);
}
