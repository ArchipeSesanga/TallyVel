namespace TallyVel.Api.Domain;

/// <summary>
/// How often a stokvel expects contributions. A closed set, not a
/// free-text field, so Cycle can never hold an unsupported or
/// misspelled value.
/// </summary>
public enum ContributionCycle
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
/// hold, and when they joined. Kept as a small record rather than a
/// bare Guid in a list, so role is captured per-membership instead of
/// assumed elsewhere.
/// </summary>
public sealed record StokvelMembership(Guid UserId, MemberRole Role, DateTimeOffset JoinedAt);

/// <summary>
/// A stokvel: a savings circle with a name, a contribution amount, a
/// cycle, and a membership list. The only way to construct one requires
/// a creator, who is automatically enrolled as the first Admin — a
/// Stokvel with zero members, or zero admins, simply cannot exist.
/// </summary>
public sealed class Stokvel
{
    public const int MaxNameLength = 100;

    public Guid Id { get; }
    public string Name { get; private set; }
    public decimal ContributionAmount { get; private set; }
    public ContributionCycle Cycle { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private readonly List<StokvelMembership> _members = new();

    /// <summary>
    /// Exposed read-only so callers can inspect membership but can only
    /// change it through AddMember / RemoveMember / ChangeRole, which
    /// enforce this Stokvel's invariants.
    /// </summary>
    public IReadOnlyCollection<StokvelMembership> Members => _members.AsReadOnly();

    public Stokvel(string name, decimal contributionAmount, ContributionCycle cycle, Guid creatorId)
    {
        Name = ValidateName(name);
        ContributionAmount = ValidateContributionAmount(contributionAmount);
        Cycle = cycle;

        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;

        // The creator is always the first member, always as Admin — a
        // Stokvel can never be created without someone able to run it.
        _members.Add(new StokvelMembership(creatorId, MemberRole.Admin, CreatedAt));
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

        _members.Add(new StokvelMembership(userId, role, DateTimeOffset.UtcNow));
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
        var index = _members.FindIndex(m => m.UserId == userId);
        if (index == -1)
            throw new InvalidOperationException("This user is not a member of this stokvel.");

        var member = _members[index];

        if (member.Role == MemberRole.Admin
            && newRole != MemberRole.Admin
            && _members.Count(m => m.Role == MemberRole.Admin) == 1)
        {
            throw new InvalidOperationException("Cannot demote the last remaining admin of a stokvel.");
        }

        _members[index] = member with { Role = newRole };
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