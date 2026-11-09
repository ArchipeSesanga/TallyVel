using Domain;

namespace Api.Application.Contracts;

public sealed record OpenCycleRequest(string Label);

public sealed record CycleResponse(Guid Id, Guid StokvelId, string Label, CycleStatus Status)
{
    public static CycleResponse FromDomain(ContributionCycle cycle) =>
        new(cycle.Id, cycle.StokvelId, cycle.Label, cycle.Status);
}

public sealed record PayoutRequest(Guid RecipientUserId);

/// <summary>A payout. Money is integer cents; PaidAt is UTC and ends in Z.</summary>
public sealed record PayoutResponse(
    Guid Id, Guid StokvelId, Guid ContributionCycleId, Guid RecipientUserId, long AmountCents, DateTimeOffset PaidAt)
{
    public static PayoutResponse FromDomain(Payout payout) =>
        new(payout.Id, payout.StokvelId, payout.ContributionCycleId, payout.RecipientUserId,
            Money.ToCents(payout.Amount), payout.PaidAt);
}

/// <summary>One member who still owes money for a cycle. Money is integer cents.</summary>
public sealed record ArrearItem(Guid MemberUserId, string FullName, long ShortfallCents);

/// <summary>
/// What closing a cycle WOULD do right now. AsOfDate is the South African calendar date the close counts up to (the cycle's
/// last day, or today if it is still running). An empty Arrears list is valid: nobody owes anything.
/// Version is opaque: send it back unchanged to close. It identifies the exact inputs of this calculation, so it changes
/// when anything that affects the result changes, and a close that carries an older one is refused (409).
/// </summary>
public sealed record ClosePreviewResponse(
    Guid CycleId,
    string CycleLabel,
    DateOnly AsOfDate,
    IReadOnlyList<ArrearItem> Arrears,
    long TotalArrearsCents,
    string Version);

/// <summary>The Version from the preview the treasurer looked at.</summary>
public sealed record CloseCycleRequest(string Version);

/// <summary>A cycle that was closed, with the arrears that were recorded.</summary>
public sealed record CloseCycleResponse(
    Guid CycleId,
    DateTimeOffset ClosedAt,
    IReadOnlyList<ArrearItem> Arrears,
    long TotalArrearsCents);
