namespace Domain;

/// <summary>
/// Stokvels run on South African dates. Every "which day is it?" question
/// goes through here, never through UTC dates or the container's local time.
/// </summary>
public static class StokvelCalendar
{
    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    /// <summary>The South African calendar date at a given instant.</summary>
    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    /// <summary>The UTC instant a cycle ending on <paramref name="endsOn"/> closes:
    /// 00:00 SAST on the next day.</summary>
    public static DateTimeOffset ClosesAt(DateOnly endsOn) =>
        new(TimeZoneInfo.ConvertTimeToUtc(
            endsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), Zone), TimeSpan.Zero);

    /// <summary>Last day of a cycle that starts on <paramref name="startsOn"/>.</summary>
    public static DateOnly EndOfCycle(DateOnly startsOn, ContributionFrequency frequency) => frequency switch
    {
        ContributionFrequency.Weekly => startsOn.AddDays(6),
        ContributionFrequency.BiWeekly => startsOn.AddDays(13),
        // Monthly cycles end on the last day of the calendar month.
        ContributionFrequency.Monthly => new DateOnly(startsOn.Year, startsOn.Month, 1).AddMonths(1).AddDays(-1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency)),
    };

    /// <summary>"2026-10" for monthly (the format contributions already use),
    /// the start date for weekly and bi-weekly.</summary>
    public static string LabelFor(DateOnly startsOn, ContributionFrequency frequency) =>
        frequency == ContributionFrequency.Monthly
            ? startsOn.ToString("yyyy-MM")
            : startsOn.ToString("yyyy-MM-dd");
}