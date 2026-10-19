namespace Domain;

/// <summary>
/// What a member still owed when a cycle closed. One row per member per
/// cycle (enforced by the database). Amounts are in cents, like the rest of
/// the money maths. RecordedAt comes from the caller so the clock stays
/// injectable.
/// </summary>
public sealed class Arrear
{
    public Guid Id { get; private init; }
    public Guid StokvelId { get; private init; }
    public Guid CycleId { get; private init; }
    public Guid MemberUserId { get; private init; }
    public long ShortfallCents { get; private init; }
    public DateTimeOffset RecordedAt { get; private init; }

    // For EF Core only.
    private Arrear()
    {
    }

    public Arrear(Guid stokvelId, Guid cycleId, Guid memberUserId, long shortfallCents, DateTimeOffset recordedAt)
    {
        if (shortfallCents <= 0)
            throw new ArgumentException("An arrear must be a positive shortfall.", nameof(shortfallCents));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleId = cycleId;
        MemberUserId = memberUserId;
        ShortfallCents = shortfallCents;
        RecordedAt = recordedAt;
    }
}
