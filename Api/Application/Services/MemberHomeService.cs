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
                // Cycles have no date, so "latest" is the greatest label
                // ("2026-10" > "2026-09"). See the README for this assumption.
                CurrentCycle = db.ContributionCycles
                    .Where(c => c.StokvelId == m.StokvelId && c.Status == CycleStatus.Open)
                    .OrderByDescending(c => c.Label)
                    .Select(c => c.Label)
                    .FirstOrDefault(),
            })
            .Select(x => new
            {
                x.StokvelId, x.Name, x.Role, x.ContributionAmount, x.Frequency, x.CreatedAt, x.CurrentCycle,
                Paid = db.Contributions
                    .Where(c => c.StokvelId == x.StokvelId && c.MemberUserId == userId && c.Cycle == x.CurrentCycle)
                    .Sum(c => (decimal?)c.Amount) ?? 0m,
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return new HomeResponse(rows.Select(r =>
        {
            var paid = r.CurrentCycle is null ? 0m : r.Paid;
            return new HomeStokvelCard(
                r.StokvelId,
                r.Name,
                r.Role,
                r.CurrentCycle,
                ToCents(paid),
                ToCents(Math.Max(0m, r.ContributionAmount - paid)),
                r.CurrentCycle is null ? null : NextPayoutDate(r.Frequency, r.CreatedAt, today));
        }).ToList());
    }

    // The one place a decimal amount becomes cents. Amounts are stored to 2
    // decimals, so the rounding only guards against a stray third decimal.
    public static long ToCents(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.ToEven);

    /// <summary>
    /// Payout dates repeat every period (week, two weeks or month) counted from
    /// the day the stokvel was created (UTC). Returns the first one on or after
    /// today. Monthly dates keep the creation day, clamped to the month's end.
    /// </summary>
    public static DateOnly NextPayoutDate(ContributionFrequency frequency, DateTimeOffset createdAt, DateOnly today)
    {
        var anchor = DateOnly.FromDateTime(createdAt.UtcDateTime);
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
