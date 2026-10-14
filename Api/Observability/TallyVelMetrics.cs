using System.Diagnostics.Metrics;

namespace Api.Observability;

// Register as a singleton: the meter is created once through IMeterFactory,
// which owns and disposes it with the app.
public sealed class TallyVelMetrics
{
    private readonly Counter<long> _contributionsRecorded;

    public TallyVelMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(TallyVelTelemetry.SourceName);
        _contributionsRecorded = meter.CreateCounter<long>(
            "tallyvel.contributions.recorded",
            unit: "{contribution}",
            description: "Contributions recorded");
    }

    public void ContributionRecorded() => _contributionsRecorded.Add(1);
}
