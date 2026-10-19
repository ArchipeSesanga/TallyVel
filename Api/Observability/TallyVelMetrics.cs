using System.Diagnostics.Metrics;

namespace Api.Observability;

public sealed class TallyVelMetrics
{
    private readonly Counter<long> _contributions;
    private readonly Counter<long> _payouts;
    private readonly Counter<long> _refusals;
    private readonly Counter<long> _cyclesClosed;
    private readonly Counter<long> _arrearsRecorded;
    private readonly Counter<long> _cycleCloseFailures;

    public TallyVelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(TallyVelTelemetry.SourceName);

        _contributions = meter.CreateCounter<long>("tallyvel.contributions.recorded",
            unit: "{contribution}", description: "Contributions recorded.");
        _payouts = meter.CreateCounter<long>("tallyvel.payouts.processed",
            unit: "{payout}", description: "Payout attempts, tagged by outcome.");
        _refusals = meter.CreateCounter<long>("tallyvel.authorization.refusals",
            unit: "{refusal}", description: "Stokvel authorization refusals, tagged by result.");
        _cyclesClosed = meter.CreateCounter<long>("tallyvel.cycles.closed",
            unit: "{cycle}", description: "Contribution cycles closed by the cycle-close job.");
        _arrearsRecorded = meter.CreateCounter<long>("tallyvel.arrears.recorded",
            unit: "{arrear}", description: "Arrears recorded when cycles closed.");
        _cycleCloseFailures = meter.CreateCounter<long>("tallyvel.cycle_close.failures",
            unit: "{failure}", description: "Cycle closes that failed and were rolled back.");
    }

    public void ContributionRecorded() => _contributions.Add(1);

    public void PayoutProcessed(string outcome) =>
        _payouts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void AuthorizationRefused(string result) =>
        _refusals.Add(1, new KeyValuePair<string, object?>("result", result));

    public void CycleClosed(int arrearsCount)
    {
        _cyclesClosed.Add(1);
        _arrearsRecorded.Add(arrearsCount);
    }

    public void CycleCloseFailed() => _cycleCloseFailures.Add(1);
}
