using Domain;

namespace Api.Application.Contracts;

/// <summary>
/// What the caller sends to record a contribution. StokvelId is
/// deliberately not a field here — it comes from the route
/// (POST /api/stokvels/{stokvelId}/contributions) so a client can never
/// send a body that disagrees with the URL it actually called.
/// The amount is integer cents (R100.50 is 10050).
/// </summary>
public sealed record RecordContributionRequest(Guid MemberUserId, string Cycle, long AmountCents);

/// <summary>A contribution. Money is integer cents; RecordedAt is UTC and ends in Z.</summary>
public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid MemberUserId,
    string Cycle,
    long AmountCents,
    ContributionStatus Status,
    DateTimeOffset RecordedAt)
{
    public static ContributionResponse FromDomain(Contribution contribution) =>
        new(
            contribution.Id,
            contribution.StokvelId,
            contribution.MemberUserId,
            contribution.Cycle,
            Money.ToCents(contribution.Amount),
            contribution.Status,
            contribution.RecordedAt);
}
