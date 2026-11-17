using Domain;

namespace Api.Application.Contracts;

/// <summary>Where a member stands in the payout order.</summary>
public enum PayoutEntryStatus
{
    /// <summary>The member has been paid out.</summary>
    PaidOut,

    /// <summary>The first member, in the order, who has not been paid out yet. At most one entry has this status.</summary>
    Next,

    /// <summary>Not paid out yet, and not the next one.</summary>
    Upcoming,
}

/// <summary>
/// One member's place in the payout order. Position starts at 1. AmountCents and PaidAt are those of the member's payout
/// (the one for THIS cycle if there is one, otherwise their most recent), and null until they have been paid. PaidAt is
/// UTC and ends in Z. IsNext is true exactly when Status is Next. IsForThisCycle says the payout shown is the one for the
/// cycle in the URL.
/// </summary>
public sealed record PayoutEntry(
    Guid MemberUserId,
    string FullName,
    int Position,
    long? AmountCents,
    DateTimeOffset? PaidAt,
    PayoutEntryStatus Status,
    bool IsNext,
    bool IsForThisCycle);

/// <summary>
/// The payout order of a stokvel, as seen from one cycle. The server decides the order (the order members joined, ties by
/// user id) and who is next (the first member in that order who has not been paid out), so no client works it out.
/// NextMemberId is null once everyone has been paid. PotCents is what a payout of this cycle is, or was: the sum of its
/// contributions that count (Paid and Confirmed). Money is integer cents.
/// </summary>
public sealed record CyclePayoutsResponse(
    Guid CycleId,
    string CycleLabel,
    CycleStatus CycleStatus,
    long PotCents,
    Guid? NextMemberId,
    IReadOnlyList<PayoutEntry> Entries);
