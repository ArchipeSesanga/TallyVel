namespace TallyVel.Api.Domain;

/// <summary>
/// Whether a collection period is still taking contributions or has
/// already been paid out to its recipient. A closed set, same rationale
/// as ContributionFrequency and MemberRole.
/// </summary>
public enum CycleStatus
{
    Open,
    PaidOut
}

/// <summary>
/// One collection period of one stokvel (e.g. "2026-09"). Contributions
/// are made into a cycle and a Payout closes it. The label uses the same
/// format as Contribution.Cycle, which is how the two are linked.
/// Whether the stokvel exists, or whether the label is unique within it,
/// is checked by whoever creates one (and enforced by the database), not
/// by this type.
/// </summary>
public sealed class ContributionCycle
{
    public const int MaxLabelLength = 20;

    public Guid Id { get; private init; }
    public Guid StokvelId { get; private init; }
    public string Label { get; private init; }
    public CycleStatus Status { get; private set; }

    // For EF Core only.
    private ContributionCycle()
    {
        Label = null!;
    }

    public ContributionCycle(Guid stokvelId, string label)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A contribution cycle must belong to a stokvel.", nameof(stokvelId));

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.", nameof(label));

        var trimmed = label.Trim();

        if (trimmed.Length > MaxLabelLength)
            throw new ArgumentException($"Label cannot exceed {MaxLabelLength} characters.", nameof(label));

        StokvelId = stokvelId;
        Label = trimmed;
        Status = CycleStatus.Open;

        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Closes the cycle once its payout has been made. A cycle can only
    /// be paid out once.
    /// </summary>
    public void MarkPaidOut()
    {
        if (Status == CycleStatus.PaidOut)
            throw new InvalidOperationException("This contribution cycle has already been paid out.");

        Status = CycleStatus.PaidOut;
    }
}
