namespace Api.Jobs;

public sealed class CycleCloseOptions
{
    public const string SectionName = "Jobs:CycleClose";

    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxCyclesPerRun { get; set; } = 50;
}
