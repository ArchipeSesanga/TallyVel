using System.Linq.Expressions;

namespace Domain;

/// <summary>
/// Where a collection period stands: Open while contributions are coming in,
/// Closed once its last day has passed (the cycle-close job set ClosedAt),
/// PaidOut once its money went to the recipient. A closed set, same rationale
/// as ContributionFrequency and MemberRole. Never stored: it is worked out by
/// <see cref="ContributionCycle.StatusOf"/>.
/// </summary>
public enum CycleStatus
{
    Open,
    Closed,
    PaidOut
}

/// <summary>
/// One collection period of one stokvel (e.g. "2026-09"). Contributions
/// are made into a cycle; when its last day (South African date) has passed,
/// the cycle-close job sets ClosedAt, records arrears and opens the next
/// cycle. A Payout pays the cycle's money out, independently of closing.
/// The label uses the same format as Contribution.Cycle, which is how the
/// two are linked. Whether the stokvel exists, or whether the label and
/// start date are unique within it, is checked by whoever creates one (and
/// enforced by the database), not by this type.
/// </summary>
public sealed class ContributionCycle
{
    public const int MaxLabelLength = 20;

    public Guid Id { get; private init; }
    public Guid StokvelId { get; private init; }
    public string Label { get; private init; }

    // The only status fact the database stores. Closed is not stored: ClosedAt says it.
    public bool IsPaidOut { get; private set; }

    public CycleStatus Status => StatusOf(IsPaidOut, ClosedAt);

    // South African calendar dates. Nullable: cycles opened before 6.4 have
    // no dates, and the cycle-close job never touches them.
    public DateOnly? StartsOn { get; private init; }
    public DateOnly? EndsOn { get; private init; }

    // When the collection period was closed. Set ONLY by the cycle-close job's
    // conditional update (WHERE "ClosedAt" IS NULL), never by C# code, so the
    // database decides who closes a cycle.
    public DateTimeOffset? ClosedAt { get; private set; }

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

        Id = Guid.NewGuid();
    }

    /// <summary>
    /// A dated cycle: runs from <paramref name="startsOn"/> to
    /// <paramref name="endsOn"/> inclusive, both South African dates.
    /// </summary>
    public ContributionCycle(Guid stokvelId, string label, DateOnly startsOn, DateOnly endsOn)
        : this(stokvelId, label)
    {
        if (endsOn < startsOn)
            throw new ArgumentException("A cycle cannot end before it starts.", nameof(endsOn));

        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    /// <summary>
    /// Records that the cycle's money has been paid out. A cycle can only
    /// be paid out once.
    /// </summary>
    public void MarkPaidOut()
    {
        if (IsPaidOut)
            throw new InvalidOperationException("This contribution cycle has already been paid out.");

        IsPaidOut = true;
    }

    /// <summary>
    /// THE status rule: PaidOut if paid out, otherwise Closed if ClosedAt is set,
    /// otherwise Open. Every status a response returns comes from here.
    /// </summary>
    public static CycleStatus StatusOf(bool isPaidOut, DateTimeOffset? closedAt) =>
        isPaidOut ? CycleStatus.PaidOut
        : closedAt is not null ? CycleStatus.Closed
        : CycleStatus.Open;

    /// <summary>
    /// "StatusOf(...) == Open" as an expression, so a query can filter on it in
    /// the database. Kept next to StatusOf; ContributionCycleStatusTests proves
    /// the two agree for every combination.
    /// </summary>
    public static readonly Expression<Func<ContributionCycle, bool>> IsOpen =
        c => !c.IsPaidOut && c.ClosedAt == null;
}