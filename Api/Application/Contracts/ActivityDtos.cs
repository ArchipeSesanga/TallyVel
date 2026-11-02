namespace Api.Application.Contracts;

/// <summary>What happened. Sent as the name (for example "ContributionRecorded").</summary>
public enum ActivityKind
{
    ContributionRecorded,
    PayoutMade,
    MemberJoined,
}

/// <summary>
/// One line in the signed-in member's activity feed. Only stokvels the member belongs to ever appear.
/// Id is unique and stable, but opaque: do not parse it. OccurredAt is UTC and ends in Z.
/// AmountCents is null when no money is involved (MemberJoined). PersonName is the member the line is about
/// (who paid, who was paid, who joined), and is null when that person is no longer a member of the stokvel.
/// </summary>
public sealed record ActivityItem(
    string Id,
    ActivityKind Kind,
    Guid StokvelId,
    string StokvelName,
    DateTimeOffset OccurredAt,
    long? AmountCents,
    string? PersonName);
