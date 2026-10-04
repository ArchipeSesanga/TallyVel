namespace TallyVel.Api.Domain;

/// <summary>
/// How often a stokvel expects contributions. A closed set, not a
/// free-text field, so Stokvel.Cycle can never hold an unsupported or
/// misspelled value. (Named Frequency, not Cycle, because ContributionCycle
/// is the entity for one specific collection period.)
/// </summary>
public enum ContributionFrequency
{
    Weekly,
    BiWeekly,
    Monthly
}

/// <summary>
/// A member's standing within one specific stokvel. Deliberately not a
/// global property on User — the same person can be an ordinary Member
/// of one stokvel and the Admin of another.
/// </summary>
public enum MemberRole
{
    Member,
    Admin
}

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

    // Navigations
    public User User { get; set; } = null!;
    public Stokvel Stokvel { get; set; } = null!;

    // Things that reference a specific membership
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
    public ICollection<Payout> Payouts { get; set; } = new List<Payout>();

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

/// <summary>
/// A stokvel: a savings circle with a name, a contribution amount, a
/// cycle, and a membership list. The only way to construct one requires
/// a creator, who is automatically enrolled as the first Admin — a
/// Stokvel with zero members, or zero admins, simply cannot exist.
/// </summary>
public sealed class Stokvel
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private init; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public ContributionFrequency Cycle { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private readonly List<StokvelMember> _members = new();

    /// <summary>
    /// Exposed read-only so callers can inspect membership but can only
    /// change it through AddMember / RemoveMember / ChangeRole, which
    /// enforce this Stokvel's invariants.
    /// </summary>
    public IReadOnlyCollection<StokvelMember> Members => _members.AsReadOnly();

    // For EF Core only.
    private Stokvel()
    {
        Name = null!;
    }

    public Stokvel(string name, decimal contributionAmount, ContributionFrequency cycle, Guid creatorId)
    {
        Name = ValidateName(name);
        ContributionAmount = ValidateContributionAmount(contributionAmount);
        Cycle = cycle;

        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;

        // The creator is always the first member, always as Admin — a
        // Stokvel can never be created without someone able to run it.
        _members.Add(new StokvelMember(Id, creatorId, MemberRole.Admin, CreatedAt));
    }

    public void Rename(string newName) => Name = ValidateName(newName);

    public void ChangeContributionAmount(decimal newAmount) =>
        ContributionAmount = ValidateContributionAmount(newAmount);

    /// <summary>
    /// Adds a member. Throws if the user is already a member — a
    /// person can only belong to a given stokvel once, so "duplicate
    /// membership" is a state that cannot occur rather than one that
    /// has to be checked for elsewhere.
    /// </summary>
    public void AddMember(Guid userId, MemberRole role = MemberRole.Member)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("This user is already a member of this stokvel.");

        _members.Add(new StokvelMember(Id, userId, role, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Removes a member, but refuses to remove the last remaining Admin
    /// — a stokvel that exists must always have someone able to
    /// administer it.
    /// </summary>
    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("This user is not a member of this stokvel.");

        if (member.Role == MemberRole.Admin && _members.Count(m => m.Role == MemberRole.Admin) == 1)
            throw new InvalidOperationException("Cannot remove the last remaining admin of a stokvel.");

        _members.Remove(member);
    }

    /// <summary>
    /// Promotes/demotes a member, but — same rule as RemoveMember —
    /// refuses to demote the last remaining Admin.
    /// </summary>
    public void ChangeRole(Guid userId, MemberRole newRole)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("This user is not a member of this stokvel.");

        if (member.Role == MemberRole.Admin
            && newRole != MemberRole.Admin
            && _members.Count(m => m.Role == MemberRole.Admin) == 1)
        {
            throw new InvalidOperationException("Cannot demote the last remaining admin of a stokvel.");
        }

        member.ChangeRole(newRole);
    }

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stokvel name is required.", nameof(name));

        var trimmed = name.Trim();

        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"Stokvel name cannot exceed {MaxNameLength} characters.", nameof(name));

        return trimmed;
    }

    private static decimal ValidateContributionAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        return Math.Round(amount, 2, MidpointRounding.ToEven);
    }

}