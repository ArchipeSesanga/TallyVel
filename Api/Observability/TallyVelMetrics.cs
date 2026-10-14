using System.Diagnostics.Metrics;

namespace Api.Observability;

public sealed class TallyVelMetrics
{
    private readonly Counter<long> _contributions;
    private readonly Counter<long> _payouts;
    private readonly Counter<long> _refusals;

    public TallyVelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(TallyVelTelemetry.SourceName);

        _contributions = meter.CreateCounter<long>("tallyvel.contributions.recorded",
            unit: "{contribution}", description: "Contributions recorded.");
        _payouts = meter.CreateCounter<long>("tallyvel.payouts.processed",
            unit: "{payout}", description: "Payout attempts, tagged by outcome.");
        _refusals = meter.CreateCounter<long>("tallyvel.authorization.refusals",
            unit: "{refusal}", description: "Stokvel authorization refusals, tagged by result.");
    }

    public void ContributionRecorded() => _contributions.Add(1);

    public void PayoutProcessed(string outcome) =>
        _payouts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void AuthorizationRefused(string result) =>
        _refusals.Add(1, new KeyValuePair<string, object?>("result", result));
}