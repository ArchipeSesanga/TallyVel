using Domain;

namespace Tests.Unit;

public class StokvelCalendarTests
{
    [Fact]
    public void A_cycle_closes_at_midnight_South_African_time_after_its_last_day()
    {
        var closesAt = StokvelCalendar.ClosesAt(new DateOnly(2026, 10, 25));

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 22, 0, 0, TimeSpan.Zero), closesAt);
    }

    [Fact]
    public void Today_is_the_South_African_date_not_the_UTC_date()
    {
        var today = StokvelCalendar.Today(new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 26), today);
    }

    [Theory]
    [InlineData(ContributionFrequency.Monthly, "2026-02-01", "2026-02-28")]
    [InlineData(ContributionFrequency.Monthly, "2026-10-19", "2026-10-31")]
    [InlineData(ContributionFrequency.Weekly, "2026-10-19", "2026-10-25")]
    [InlineData(ContributionFrequency.BiWeekly, "2026-10-19", "2026-11-01")]
    public void Cycle_end_depends_on_frequency(ContributionFrequency frequency, string startsOn, string expected)
    {
        var end = StokvelCalendar.EndOfCycle(DateOnly.Parse(startsOn), frequency);

        Assert.Equal(DateOnly.Parse(expected), end);
    }
}
