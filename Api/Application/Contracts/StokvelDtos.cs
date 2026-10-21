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

/// <summary>The contribution amount is integer cents (R500 is 50000).</summary>
public sealed record CreateStokvelRequest(string Name, long ContributionAmountCents, ContributionFrequency Cycle, Guid CreatorId);

public sealed record AddStokvelMemberRequest(Guid UserId, MemberRole Role = MemberRole.Member);

public sealed record ChangeStokvelMemberRoleRequest(MemberRole Role);
