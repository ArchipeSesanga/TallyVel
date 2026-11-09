using System.Diagnostics;
using Api.Application.Services;
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

        // The arrears and the exactly-once claim live in CycleClosing, shared with the treasurer's preview and close.
        var outcome = await CycleClosing.CloseAsync(db, cycleId, clock.GetUtcNow(), expectedVersion: null, ct);
        if (outcome.Status != CloseStatus.Closed)
        {
            activity?.SetTag("tallyvel.cycle.outcome", "skipped");
            return (Outcome.Skipped, 0);
        }

        var arrears = outcome.Calculation!.Arrears.Count;
        metrics.CycleClosed(arrears);
        logger.LogInformation("Closed cycle {CycleId} with {ArrearsCount} arrears", cycleId, arrears);
        activity?.SetTag("tallyvel.cycle.outcome", "closed");
        return (Outcome.Closed, arrears);
    }
}
