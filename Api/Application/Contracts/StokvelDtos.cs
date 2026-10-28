using Domain;

namespace Api.Application.Contracts;

public sealed record StokvelMembershipResponse(Guid UserId, MemberRole Role, DateTimeOffset JoinedAt)
{
    public static StokvelMembershipResponse FromDomain(StokvelMember membership) =>
        new(membership.UserId, membership.Role, membership.JoinedAt);
}

public sealed record StokvelResponse(
    Guid Id,
    string Name,
    long ContributionAmountCents,
    ContributionFrequency Cycle,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<StokvelMembershipResponse> Members)
{
    public static StokvelResponse FromDomain(Stokvel stokvel) =>
        new(
            stokvel.Id,
            stokvel.Name,
            Money.ToCents(stokvel.ContributionAmount),
            stokvel.Cycle,
            stokvel.CreatedAt,
            stokvel.Members.Select(StokvelMembershipResponse.FromDomain).ToList());
}

/// <summary>
/// One stokvel with the numbers its page shows. Money is integer cents; EndsOn is a
/// South African calendar date. CurrentCycle is null when no cycle is open.
/// </summary>
public sealed record StokvelDetailResponse(
    Guid Id,
    string Name,
    long ContributionAmountCents,
    ContributionFrequency Cycle,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<StokvelMembershipResponse> Members,
    int MemberCount,
    StokvelCurrentCycle? CurrentCycle)
{
    public static StokvelDetailResponse FromDomain(Stokvel stokvel, StokvelSummary summary) =>
        new(
            stokvel.Id,
            stokvel.Name,
            Money.ToCents(stokvel.ContributionAmount),
            stokvel.Cycle,
            stokvel.CreatedAt,
            stokvel.Members.Select(StokvelMembershipResponse.FromDomain).ToList(),
            summary.MemberCount,
            summary.CurrentCycle);
}

/// <summary>
/// The open cycle. CollectedCents is what has arrived (Paid and Confirmed contributions);
/// DueCents is what the whole stokvel owes for the cycle (members times the contribution
/// amount). EndsOn is null only for a cycle created before cycles had dates.
/// </summary>
public sealed record StokvelCurrentCycle(
    Guid Id,
    string Label,
    CycleStatus Status,
    DateOnly? EndsOn,
    long CollectedCents,
    long DueCents);

public sealed record StokvelSummary(int MemberCount, StokvelCurrentCycle? CurrentCycle);

/// <summary>The contribution amount is integer cents (R500 is 50000).</summary>
public sealed record CreateStokvelRequest(string Name, long ContributionAmountCents, ContributionFrequency Cycle, Guid CreatorId);

public sealed record AddStokvelMemberRequest(Guid UserId, MemberRole Role = MemberRole.Member);

public sealed record ChangeStokvelMemberRoleRequest(MemberRole Role);
