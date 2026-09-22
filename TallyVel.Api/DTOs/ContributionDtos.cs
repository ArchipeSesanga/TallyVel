using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

/// <summary>
/// What the caller sends to record a contribution. StokvelId is
/// deliberately not a field here — it comes from the route
/// (POST /api/stokvels/{stokvelId}/contributions) so a client can never
/// send a body that disagrees with the URL it actually called.
/// </summary>
public sealed record RecordContributionRequest(Guid MemberUserId, string Cycle, decimal Amount);

public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid MemberUserId,
    string Cycle,
    decimal Amount,
    DateTimeOffset RecordedAt)
{
    public static ContributionResponse FromDomain(Contribution contribution) =>
        new(
            contribution.Id,
            contribution.StokvelId,
            contribution.MemberUserId,
            contribution.Cycle,
            contribution.Amount,
            contribution.RecordedAt);
}
