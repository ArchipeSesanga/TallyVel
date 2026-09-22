namespace TallyVel.Api.Domain;

/// <summary>
/// A single payment made by one member toward one stokvel. Amount and
/// contributor are validated up front — same rationale as Stokvel and
/// User — so an invalid Contribution can never exist. The record only
/// captures the payment itself; whether the contributor is actually a
/// member of the stokvel is checked by whoever creates one
/// (see ContributionService), not by this type.
/// </summary>
public sealed class Contribution
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public DateTimeOffset CreatedAt { get; }

    public Contribution(Guid stokvelId, Guid contributorId, decimal amount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A contribution must belong to a stokvel.", nameof(stokvelId));

        if (contributorId == Guid.Empty)
            throw new ArgumentException("A contribution must have a contributor.", nameof(contributorId));

        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        StokvelId = stokvelId;
        ContributorId = contributorId;
        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);

        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
