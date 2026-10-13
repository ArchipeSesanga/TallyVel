using Domain;

namespace Api.Application.Contracts;

public sealed record OpenCycleRequest(string Label);

public sealed record CycleResponse(Guid Id, Guid StokvelId, string Label, CycleStatus Status)
{
    public static CycleResponse FromDomain(ContributionCycle cycle) =>
        new(cycle.Id, cycle.StokvelId, cycle.Label, cycle.Status);
}

public sealed record PayoutRequest(Guid RecipientUserId);

public sealed record PayoutResponse(
    Guid Id, Guid StokvelId, Guid ContributionCycleId, Guid RecipientUserId, decimal Amount, DateTimeOffset PaidAt)
{
    public static PayoutResponse FromDomain(Payout payout) =>
        new(payout.Id, payout.StokvelId, payout.ContributionCycleId, payout.RecipientUserId, payout.Amount, payout.PaidAt);
}
