using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>Works out the numbers a stokvel page shows, so the page adds nothing up.</summary>
public sealed class StokvelSummaryService(TallyVelDbContext db, TimeProvider clock)
{
    public async Task<StokvelSummary> GetAsync(Stokvel stokvel, CancellationToken ct)
    {
        var today = StokvelCalendar.Today(clock.GetUtcNow());

        // One query. The member count, and the money for the current cycle, are summed by the
        // database. The current cycle is picked the same way as on the home screen: the open
        // cycle that has already started; if several are open, the greatest label wins.
        var row = await db.Stokvels
            .AsNoTracking()
            .Where(s => s.Id == stokvel.Id)
            .Select(s => new
            {
                MemberCount = db.StokvelMembers.Count(m => m.StokvelId == s.Id),
                Cycle = db.ContributionCycles
                    .Where(c => c.StokvelId == s.Id)
                    .Where(ContributionCycle.IsOpen)
                    .Where(c => c.StartsOn == null || c.StartsOn <= today)
                    .OrderByDescending(c => c.Label)
                    .Select(c => new { c.Id, c.Label, c.EndsOn, c.IsPaidOut, c.ClosedAt })
                    .FirstOrDefault(),
            })
            .Select(x => new
            {
                x.MemberCount,
                x.Cycle,
                // Only contributions that count (ContributionRules): Pending does not.
                Collected = db.Contributions
                    .Where(ContributionRules.Counts)
                    .Where(c => x.Cycle != null && c.StokvelId == stokvel.Id && c.Cycle == x.Cycle.Label)
                    .Sum(c => (decimal?)c.Amount) ?? 0m,
            })
            .SingleAsync(ct);

        if (row.Cycle is not { } cycle)
            return new StokvelSummary(row.MemberCount, null);

        // Everyone in the stokvel owes one contribution for the cycle.
        var dueCents = Money.ToCents(stokvel.ContributionAmount) * row.MemberCount;

        return new StokvelSummary(
            row.MemberCount,
            new StokvelCurrentCycle(
                cycle.Id,
                cycle.Label,
                ContributionCycle.StatusOf(cycle.IsPaidOut, cycle.ClosedAt),
                cycle.EndsOn,
                Money.ToCents(row.Collected),
                dueCents));
    }
}
