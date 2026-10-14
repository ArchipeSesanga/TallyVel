using System.Diagnostics;

namespace Api.Observability;

public static class TallyVelTelemetry
{
    public const string ServiceName = "tallyvel-api";
    public const string SourceName = "TallyVel";   // ActivitySource and Meter share it

    // Static: an ActivitySource is created once and lives for the whole app.
    public static readonly ActivitySource Source = new(SourceName);
}