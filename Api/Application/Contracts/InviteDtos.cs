using Domain;

namespace Api.Application.Contracts;

/// <summary>A new invite. The code is the whole secret: anyone who has it can read the preview and join.</summary>
public sealed record InviteResponse(string Code, DateTimeOffset ExpiresAt);

/// <summary>
/// What a person sees before they join, and ONLY this. It has no member names, no user ids, no balances,
/// no contributions and no payout order: the caller is not a member yet, so none of that is theirs to see.
/// </summary>
/// <param name="Frequency">How often a cycle comes round.</param>
/// <param name="CycleLengthDays">7 or 14 for weekly and bi-weekly; null for monthly (a calendar month).</param>
/// <param name="AlreadyMember">Whether the caller is already in this stokvel.</param>
public sealed record InvitePreviewResponse(
    string Code,
    string StokvelName,
    string? Rules,
    long ContributionAmountCents,
    ContributionFrequency Frequency,
    int? CycleLengthDays,
    int MemberCount,
    DateTimeOffset ExpiresAt,
    bool AlreadyMember);

public sealed record JoinResponse(Guid StokvelId);
