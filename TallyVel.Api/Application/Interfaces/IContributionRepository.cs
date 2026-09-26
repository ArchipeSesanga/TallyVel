using TallyVel.Api.Domain;

namespace TallyVel.Api.Application.Interfaces;

/// <summary>
/// An abstraction over where Contributions are stored. Same rationale
/// as <see cref="IStokvelRepository"/>.
/// </summary>
public interface IContributionRepository
{
    IReadOnlyCollection<Contribution> GetAll();
    Contribution? GetById(Guid id);
    void Add(Contribution contribution);

    /// <summary>
    /// True if this member already has a contribution recorded for this
    /// exact stokvel + cycle. This is what makes "one contribution per
    /// member per cycle" an enforceable rule instead of just a
    /// convention — ContributionService relies on this to reject a
    /// second payment for a cycle that's already covered.
    /// </summary>
    bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle);
}
