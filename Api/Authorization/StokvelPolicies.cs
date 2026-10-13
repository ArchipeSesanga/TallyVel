using Domain;

namespace Api.Authorization;

/// <summary>
/// Policy names for stokvel-scoped access. Controllers refer to these
/// constants, never to role names.
/// </summary>
public static class StokvelPolicies
{
    public const string Member = "StokvelMember";
    public const string Treasurer = "StokvelTreasurer";
    public const string Chairperson = "StokvelChairperson";

    // Which stokvel roles satisfy each policy. A higher role includes the lower ones.
    public static readonly IReadOnlyDictionary<string, MemberRole[]> AllowedRoles =
        new Dictionary<string, MemberRole[]>
        {
            [Member] = [MemberRole.Member, MemberRole.Treasurer, MemberRole.Chairperson],
            [Treasurer] = [MemberRole.Treasurer, MemberRole.Chairperson],
            [Chairperson] = [MemberRole.Chairperson],
        };
}
