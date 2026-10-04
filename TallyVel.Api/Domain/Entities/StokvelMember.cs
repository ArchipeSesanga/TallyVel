namespace TallyVel.Api.Domain;

/// <summary>
/// One member's standing inside a Stokvel: who they are, what role they
/// hold, and when they joined. Kept as its own small type rather than a
/// bare Guid in a list, so role is captured per-membership instead of
/// assumed elsewhere. A class rather than a record because EF Core
/// tracks it as an entity (keyed by StokvelId + UserId) and a role
/// change mutates it in place instead of replacing it.
/// </summary>
public sealed class StokvelMember
{
    // Composite key parts (also FKs)
    public Guid StokvelId { get; private init; }
    public Guid UserId { get; private init; }

    // Data the relationship carries — the reason this entity exists
    public MemberRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private init; }

    // Navigations (set by EF Core; changed only through the aggregate)
    public User User { get; private set; } = null!;
    public Stokvel Stokvel { get; private set; } = null!;

    // Payouts made to this membership. Contributions are deliberately not
    // navigable from here: a contribution is tied to a user and a cycle, not
    // to a membership, so a contributor need not (still) be a member.
    public ICollection<Payout> Payouts { get; private set; } = new List<Payout>();

    // For EF Core only.
    private StokvelMember()
    {
    }

    internal StokvelMember(Guid stokvelId, Guid userId, MemberRole role, DateTimeOffset joinedAt)
    {
        StokvelId = stokvelId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    internal void ChangeRole(MemberRole newRole) => Role = newRole;
}
