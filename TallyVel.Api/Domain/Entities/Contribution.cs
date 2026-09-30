namespace TallyVel.Api.Domain;

/// <summary>
/// A single payment made by one member toward one stokvel, for one
/// contribution cycle (e.g. "2026-09"). Amount, cycle and the member are
/// validated up front — same rationale as Stokvel and User — so an
/// invalid Contribution can never exist. The record only captures the
/// payment itself; whether the member is actually part of the stokvel,
/// and whether they've already paid for this cycle, are checked by
/// whoever creates one (see IContributionRepository.RecordContributionAsync),
/// not by this type.
/// </summary>
public sealed class Contribution
{
    public Guid Id { get; private init; }
    public Guid StokvelId { get; private init; }
    public Guid MemberUserId { get; private init; }
    public string Cycle { get; private init; }
    public decimal Amount { get; private init; }
    public DateTimeOffset RecordedAt { get; private init; }

    // For EF Core only.
    private Contribution()
    {
        Cycle = null!;
    }

    public Contribution(Guid stokvelId, Guid memberUserId, string cycle, decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A contribution must belong to a stokvel.", nameof(stokvelId));

        if (memberUserId == Guid.Empty)
            throw new ArgumentException("A contribution must have a contributing member.", nameof(memberUserId));

        if (string.IsNullOrWhiteSpace(cycle))
            throw new ArgumentException("Cycle is required.", nameof(cycle));

        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        StokvelId = stokvelId;
        MemberUserId = memberUserId;
        Cycle = cycle.Trim();
        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);

        Id = Guid.NewGuid();
        RecordedAt = DateTimeOffset.UtcNow;
    }
}
