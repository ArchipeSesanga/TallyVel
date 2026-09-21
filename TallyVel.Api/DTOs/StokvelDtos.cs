using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

public sealed record StokvelMembershipResponse(Guid UserId, MemberRole Role, DateTimeOffset JoinedAt)
{
    public static StokvelMembershipResponse FromDomain(StokvelMembership membership) =>
        new(membership.UserId, membership.Role, membership.JoinedAt);
}

public sealed record StokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    ContributionCycle Cycle,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<StokvelMembershipResponse> Members)
{
    public static StokvelResponse FromDomain(Stokvel stokvel) =>
        new(
            stokvel.Id,
            stokvel.Name,
            stokvel.ContributionAmount,
            stokvel.Cycle,
            stokvel.CreatedAt,
            stokvel.Members.Select(StokvelMembershipResponse.FromDomain).ToList());
}

public sealed record CreateStokvelRequest(string Name, decimal ContributionAmount, ContributionCycle Cycle, Guid CreatorId);

public sealed record AddStokvelMemberRequest(Guid UserId, MemberRole Role = MemberRole.Member);

public sealed record ChangeStokvelMemberRoleRequest(MemberRole Role);
