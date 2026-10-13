namespace Domain;

public interface ICycleRepository
{
    Task<IReadOnlyList<ContributionCycle>> ListAsync(Guid stokvelId, CancellationToken ct);

    /// <summary>Opens a new cycle. A duplicate label in the same stokvel is a unique violation (409).</summary>
    Task<ContributionCycle> OpenAsync(Guid stokvelId, string label, CancellationToken ct);

    /// <summary>
    /// Pays the cycle's total contributions to one member and closes the cycle.
    /// Throws NotFoundException if the cycle is not in this stokvel, and
    /// BusinessRuleViolationException if the recipient is not a member, nothing was
    /// contributed, or the cycle was already paid out.
    /// </summary>
    Task<Payout> PayOutAsync(Guid stokvelId, Guid cycleId, Guid recipientUserId, CancellationToken ct);
}
