using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>Builds the member home screen in one database round trip.</summary>
public sealed class MemberHomeService(TallyVelDbContext db, TimeProvider clock)
{
    public async Task<HomeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var today = StokvelCalendar.Today(clock.GetUtcNow());

        // One query: the memberships, each with its current cycle and the
        // caller's own total for it, as correlated subqueries (no N+1).
        var rows = await db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.StokvelId,
                m.Stokvel.Name,
                m.Role,
                m.Stokvel.ContributionAmount,
                Frequency = m.Stokvel.Cycle,
                m.Stokvel.CreatedAt,
                // The current cycle is the open one that has already started
                // (an undated legacy cycle counts as started). If several are
                // open, the greatest label wins ("2026-10" > "2026-09").
                // See the README for this assumption.
                CurrentCycle = db.ContributionCycles
                    .Where(c => c.StokvelId == m.StokvelId)
                    .Where(ContributionCycle.IsOpen)
                    .Where(c => c.StartsOn == null || c.StartsOn <= today)
                    .OrderByDescending(c => c.Label)
                    .Select(c => new { c.Id, c.Label, c.StartsOn, c.EndsOn, c.IsPaidOut, c.ClosedAt })
                    .FirstOrDefault(),
            })
            .Select(x => new
            {
                x.StokvelId, x.Name, x.Role, x.ContributionAmount, x.Frequency, x.CreatedAt, x.CurrentCycle,
                // Only contributions that count (ContributionRules): Pending does not.
                Paid = db.Contributions
                    .Where(ContributionRules.Counts)
                    .Where(c => c.StokvelId == x.StokvelId && c.MemberUserId == userId
                                && x.CurrentCycle != null && c.Cycle == x.CurrentCycle.Label)
                    .Sum(c => (decimal?)c.Amount) ?? 0m,
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return new HomeResponse(rows.Select(r =>
        {
            // No open cycle: nothing is being collected, so nothing is paid or owed.
            if (r.CurrentCycle is not { } cycle)
                return new HomeStokvelCard(r.StokvelId, r.Name, r.Role, null, 0, 0, null);

            var paidCents = Money.ToCents(r.Paid);
            var owedCents = Math.Max(0, Money.ToCents(r.ContributionAmount) - paidCents);
            return new HomeStokvelCard(
                r.StokvelId,
                r.Name,
                r.Role,
                new HomeCurrentCycle(
                    cycle.Id,
                    cycle.Label,
                    ContributionCycle.StatusOf(cycle.IsPaidOut, cycle.ClosedAt),
                    cycle.StartsOn,
                    cycle.EndsOn),
                paidCents,
                owedCents,
                NextPayoutDate(r.Frequency, r.CreatedAt, today));
        }).ToList());
    }

    // Kept so existing callers and tests have one name; the conversion itself lives in Money.
    public static long ToCents(decimal amount) =>
        Money.ToCents(amount);

    /// <summary>
    /// Payout dates repeat every period (week, two weeks or month) counted from
    /// the day the stokvel was created, as a South African calendar date.
    /// Returns the first one on or after <paramref name="today"/> (also a South
    /// African date). Monthly dates keep the creation day, clamped to the month's end.
    /// </summary>
    public static DateOnly NextPayoutDate(ContributionFrequency frequency, DateTimeOffset createdAt, DateOnly today)
    {
        var anchor = StokvelCalendar.Today(createdAt);
        if (anchor >= today)
            return anchor;

        if (frequency == ContributionFrequency.Monthly)
        {
            var months = (today.Year - anchor.Year) * 12 + today.Month - anchor.Month;
            var candidate = anchor.AddMonths(months);
            return candidate >= today ? candidate : anchor.AddMonths(months + 1);
        }

        var periodDays = frequency == ContributionFrequency.Weekly ? 7 : 14;
        var elapsed = today.DayNumber - anchor.DayNumber;
        var periods = (elapsed + periodDays - 1) / periodDays; // round up
        return anchor.AddDays(periods * periodDays);
    }
}
