using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Application.Interfaces;

/// <summary>
/// An abstraction over where Contributions are stored. Same rationale
/// as <see cref="IStokvelRepository"/>.
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
    /// convention — this is what the repository relies on to reject a
    /// second payment for a cycle that's already covered.
    /// </summary>
    bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle);

    /// <summary>
    /// Validates and records a contribution idempotently: reserves the
    /// idempotency key, checks the stokvel/member exist and that the
    /// member hasn't already paid for this cycle, then writes the
    /// contribution.
    /// </summary>
    Task<ContributionResponse> RecordContributionAsync(
        Guid stokvelId,
        RecordContributionRequest request,
        string? idempotencyKey,
        CancellationToken ct = default);






}
