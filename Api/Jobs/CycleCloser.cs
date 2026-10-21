using System.Diagnostics;
using Api.Observability;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Jobs;

public sealed record CycleCloseResult(int Closed, int Skipped, int Failed, int ArrearsRecorded, int Attempts);

/// <summary>
/// Closes every cycle whose last day (South African date) has passed: records
/// who still owes money and opens the next cycle. "Exactly once" is decided by
/// the database: the close is claimed with a conditional UPDATE and the row
/// count says who won.
/// </summary>
public sealed class CycleCloser(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<CycleCloseOptions> options,
    TallyVelMetrics metrics,
    ILogger<CycleCloser> logger)
{
    private enum Outcome { Closed, Skipped }

    public async Task<CycleCloseResult> RunOnceAsync(CancellationToken ct)
    {
        using var run = TallyVelTelemetry.Source.StartActivity("cycles.close_run");

        int closed = 0, skipped = 0, failed = 0, arrears = 0, attempts = 0;
        var failedIds = new HashSet<Guid>();
        var max = options.Value.MaxCyclesPerRun;

        while (attempts < max)
        {
            var today = StokvelCalendar.Today(clock.GetUtcNow());

            List<(Guid Id, Guid StokvelId)> due;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TallyVelDbContext>();
                var rows = await db.ContributionCycles
                    .AsNoTracking()
                    .Where(c => c.EndsOn != null && c.EndsOn < today && c.ClosedAt == null)
                    .OrderBy(c => c.EndsOn)
                    .Select(c => new { c.Id, c.StokvelId })
                    .Take(max + failedIds.Count)
                    .ToListAsync(ct);
                due = rows.Where(r => !failedIds.Contains(r.Id)).Select(r => (r.Id, r.StokvelId)).ToList();
            }

            if (due.Count == 0)
                break;

            foreach (var (cycleId, stokvelId) in due)
            {
                if (attempts >= max)
                    break;
                attempts++;

                try
                {
                    var (outcome, count) = await CloseOneAsync(cycleId, ct);
                    if (outcome == Outcome.Closed) { closed++; arrears += count; }
                    else skipped++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedIds.Add(cycleId);
                    failed++;
                    metrics.CycleCloseFailed();
                    logger.LogError(ex, "Closing cycle {CycleId} of stokvel {StokvelId} failed; it will be retried next run",
                        cycleId, stokvelId);
                }
            }
        }

        run?.SetTag("tallyvel.cycles.closed", closed);
        run?.SetTag("tallyvel.cycles.failed", failed);
        return new CycleCloseResult(closed, skipped, failed, arrears, attempts);
    }

    private async Task<(Outcome, int ArrearsCount)> CloseOneAsync(Guid cycleId, CancellationToken ct)
    {
        using var activity = TallyVelTelemetry.Source.StartActivity("cycle.close");
        activity?.SetTag("tallyvel.cycle.id", cycleId);

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TallyVelDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();

        // CLAIM: only one caller can flip ClosedAt from NULL; the row count decides.
        var claimed = await db.ContributionCycles
            .Where(c => c.Id == cycleId && c.ClosedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAt, now), ct);
        if (claimed == 0)
        {
            activity?.SetTag("tallyvel.cycle.outcome", "skipped");
            return (Outcome.Skipped, 0);
        }

        var cycle = await db.ContributionCycles.AsNoTracking().SingleAsync(c => c.Id == cycleId, ct);
        var stokvel = await db.Stokvels.AsNoTracking()
            .Where(s => s.Id == cycle.StokvelId)
            .Select(s => new { s.ContributionAmount, s.Cycle })
            .SingleAsync(ct);

        var endsOn = cycle.EndsOn!.Value;
        var closesAt = StokvelCalendar.ClosesAt(endsOn);
        var label = cycle.Label;
        var stokvelId = cycle.StokvelId;

        // Members who had joined before the cycle ended, with what each paid, in one query.
        var members = await db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && m.JoinedAt < closesAt)
            .Select(m => new
            {
                m.UserId,
                Paid = db.Contributions
                    .Where(ContributionRules.Counts)
                    .Where(c => c.StokvelId == stokvelId && c.MemberUserId == m.UserId && c.Cycle == label)
                    .Sum(c => (decimal?)c.Amount) ?? 0m,
            })
            .ToListAsync(ct);

        var dueCents = Money.ToCents(stokvel.ContributionAmount);
        var arrears = new List<Arrear>();
        foreach (var m in members)
        {
            var shortfall = dueCents - Money.ToCents(m.Paid);
            if (shortfall > 0)
                arrears.Add(new Arrear(stokvelId, cycleId, m.UserId, shortfall, now));
        }
        db.Arrears.AddRange(arrears);

        // Open the next cycle unless one already covers it.
        var nextStart = endsOn.AddDays(1);
        var nextLabel = StokvelCalendar.LabelFor(nextStart, stokvel.Cycle);
        var nextExists = await db.ContributionCycles.AnyAsync(c =>
            c.StokvelId == stokvelId && (c.StartsOn >= nextStart || c.Label == nextLabel), ct);
        if (!nextExists)
            db.ContributionCycles.Add(new ContributionCycle(
                stokvelId, nextLabel, nextStart, StokvelCalendar.EndOfCycle(nextStart, stokvel.Cycle)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        metrics.CycleClosed(arrears.Count);
        logger.LogInformation("Closed cycle {CycleId} of stokvel {StokvelId} with {ArrearsCount} arrears",
            cycleId, stokvelId, arrears.Count);
        activity?.SetTag("tallyvel.cycle.outcome", "closed");
        return (Outcome.Closed, arrears.Count);
    }
}
