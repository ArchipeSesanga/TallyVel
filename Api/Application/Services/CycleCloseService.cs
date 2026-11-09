using Api.Application.Contracts;
using Api.Observability;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>The treasurer's side of closing a cycle: look first (preview), then close with what was seen.</summary>
public sealed class CycleCloseService(
    TallyVelDbContext db, TimeProvider clock, IdempotentExecutor idempotency, TallyVelMetrics metrics,
    ILogger<CycleCloseService> logger)
{
    private static AlreadyExistsException AlreadyClosed() =>
        new("cycle-already-closed", "This cycle has already been closed.");

    /// <summary>The cycle, if it belongs to this stokvel; otherwise a 404 that is the same as "no such cycle".</summary>
    private async Task<ContributionCycle> CycleOfAsync(Guid stokvelId, Guid cycleId, CancellationToken ct) =>
        await db.ContributionCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
            ?? throw new NotFoundException("cycle", cycleId);

    public async Task<ClosePreviewResponse> PreviewAsync(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await CycleOfAsync(stokvelId, cycleId, ct);
        if (cycle.ClosedAt is not null) throw AlreadyClosed();

        // The SAME function the real close uses.
        var calculation = await CycleClosing.CalculateArrearsAsync(db, cycleId, StokvelCalendar.Today(clock.GetUtcNow()), ct);
        return new ClosePreviewResponse(
            calculation.CycleId, calculation.CycleLabel, calculation.AsOfDate,
            calculation.Arrears.Select(a => new ArrearItem(a.MemberUserId, a.FullName, a.ShortfallCents)).ToList(),
            calculation.TotalArrearsCents, calculation.Version);
    }

    public Task<CloseCycleResponse> CloseAsync(
        Guid stokvelId, Guid cycleId, string version, string? idempotencyKey, CancellationToken ct) =>
        idempotency.RunAsync(idempotencyKey, new { stokvelId, cycleId, version }, StatusCodes.Status200OK, async () =>
        {
            var cycle = await CycleOfAsync(stokvelId, cycleId, ct);
            if (cycle.ClosedAt is not null) throw AlreadyClosed(); // closed by the nightly job or another treasurer

            var outcome = await CycleClosing.CloseAsync(db, cycleId, clock.GetUtcNow(), version, ct);
            switch (outcome.Status)
            {
                case CloseStatus.AlreadyClosed:
                    throw AlreadyClosed(); // lost the race: someone closed it between our check and our claim
                case CloseStatus.Stale:
                    throw new AlreadyExistsException("close-preview-stale",
                        "Something changed since you looked (a payment, a member or the date). Nothing was closed. Review the new preview and try again.");
            }

            var calculation = outcome.Calculation!;
            metrics.CycleClosed(calculation.Arrears.Count);
            logger.LogInformation("Cycle {CycleId} closed by a treasurer with {ArrearsCount} arrears", cycleId, calculation.Arrears.Count);
            return new CloseCycleResponse(
                cycleId, outcome.ClosedAt!.Value,
                calculation.Arrears.Select(a => new ArrearItem(a.MemberUserId, a.FullName, a.ShortfallCents)).ToList(),
                calculation.TotalArrearsCents);
        }, ct);
}
