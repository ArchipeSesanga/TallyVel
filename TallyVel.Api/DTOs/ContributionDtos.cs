using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid ContributorId,
    decimal Amount,
    DateTimeOffset CreatedAt)
{
    public static ContributionResponse FromDomain(Contribution contribution) =>
        new(contribution.Id, contribution.StokvelId, contribution.ContributorId, contribution.Amount, contribution.CreatedAt);
}

public sealed record AddContributionRequest(Guid StokvelId, Guid ContributorId, decimal Amount);
