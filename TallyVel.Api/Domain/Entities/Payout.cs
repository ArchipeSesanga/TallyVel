namespace TallyVel.Api.Domain;

/// <summary>
/// The payout of one contribution cycle to one member of the stokvel.
/// Amount and the ids are validated up front — same rationale as
/// Contribution — so an invalid Payout can never exist. Whether the
/// recipient is actually a member, and that the cycle has not already
/// been paid out, are checked by whoever creates one (and the one-payout-
/// per-cycle rule is enforced by the database), not by this type.
/// </summary>
public sealed class Payout
{
    public Guid Id { get; private init; }
    public Guid StokvelId { get; private init; }
    public Guid ContributionCycleId { get; private init; }
    public Guid RecipientUserId { get; private init; }
    public decimal Amount { get; private init; }
    public DateTimeOffset PaidAt { get; private init; }

    // For EF Core only.
    private Payout()
    {
    }

    public Payout(Guid stokvelId, Guid contributionCycleId, Guid recipientUserId, decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A payout must belong to a stokvel.", nameof(stokvelId));

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException("A payout must be for a contribution cycle.", nameof(contributionCycleId));

        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("A payout must have a recipient.", nameof(recipientUserId));

        if (amount <= 0)
            throw new ArgumentException("Payout amount must be greater than zero.", nameof(amount));

        StokvelId = stokvelId;
        ContributionCycleId = contributionCycleId;
        RecipientUserId = recipientUserId;
        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);

        Id = Guid.NewGuid();
        PaidAt = DateTimeOffset.UtcNow;
    }
}
