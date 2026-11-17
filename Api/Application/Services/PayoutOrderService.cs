using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>
/// Works out the payout order of a stokvel and who is next, so a client never does.
///
/// The rule, in one place:
///  - Order: members by when they joined, then by user id (so equal join times still give one fixed order).
///  - Paid out: the member has at least one payout in this stokvel.
///  - Next: the first member in the order who is not paid out. Nobody is next once everyone has been paid.
/// Nothing stops a treasurer paying someone out of turn (the payout endpoint takes any member), so "next" is the order's
/// answer, not a lock.
/// </summary>
public sealed class PayoutOrderService(TallyVelDbContext db)
{
    public async Task<CyclePayoutsResponse> GetAsync(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
            ?? throw new NotFoundException("cycle", cycleId);

        var members = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId)
            .Select(m => new { m.UserId, m.JoinedAt, m.User.FullName })
            .ToListAsync(ct);
        var ordered = members.OrderBy(m => m.JoinedAt).ThenBy(m => m.UserId).ToList();

        var payouts = (await db.Payouts.AsNoTracking().Where(p => p.StokvelId == stokvelId).ToListAsync(ct))
            .GroupBy(p => p.RecipientUserId)
            .ToDictionary(
                g => g.Key,
                // The payout for THIS cycle wins; otherwise the most recent one.
                g => g.FirstOrDefault(p => p.ContributionCycleId == cycleId) ?? g.OrderByDescending(p => p.PaidAt).First());

        var nextMemberId = ordered.FirstOrDefault(m => !payouts.ContainsKey(m.UserId))?.UserId;

        var entries = ordered.Select((m, index) =>
        {
            payouts.TryGetValue(m.UserId, out var payout);
            var status = payout is not null ? PayoutEntryStatus.PaidOut
                : m.UserId == nextMemberId ? PayoutEntryStatus.Next
                : PayoutEntryStatus.Upcoming;

            return new PayoutEntry(
                m.UserId,
                m.FullName,
                index + 1,
                payout is null ? null : Money.ToCents(payout.Amount),
                payout?.PaidAt,
                status,
                status == PayoutEntryStatus.Next,
                payout?.ContributionCycleId == cycleId);
        }).ToList();

        var pot = await db.Contributions.AsNoTracking()
            .Where(ContributionRules.Counts)
            .Where(c => c.StokvelId == stokvelId && c.Cycle == cycle.Label)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        return new CyclePayoutsResponse(
            cycle.Id, cycle.Label, cycle.Status, Money.ToCents(pot), nextMemberId, entries);
    }
}
