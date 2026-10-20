using Api.Application.Services;
using Domain;

namespace Tests.Unit;

public class NextPayoutDateTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 31, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ContributionFrequency.Weekly, "2026-10-14", "2026-10-17")]   // Saturdays from 31 Jan
    [InlineData(ContributionFrequency.Weekly, "2026-10-17", "2026-10-17")]   // payout day itself counts
    [InlineData(ContributionFrequency.BiWeekly, "2026-10-14", "2026-10-24")]
    [InlineData(ContributionFrequency.Monthly, "2026-10-14", "2026-10-31")]
    [InlineData(ContributionFrequency.Monthly, "2026-11-01", "2026-11-30")]  // 31st clamps to month end
    [InlineData(ContributionFrequency.Monthly, "2026-01-10", "2026-01-31")]  // before the first payout
    public void Is_the_first_period_boundary_on_or_after_today(ContributionFrequency frequency, string today, string expected)
    {
        var next = MemberHomeService.NextPayoutDate(frequency, Created, DateOnly.Parse(today));

        Assert.Equal(DateOnly.Parse(expected), next);
    }

    [Theory]
    // Created 22:30 UTC on 14 Oct = 00:30 on 15 Oct in Johannesburg. The anchor is the
    // South African date, 15 Oct; a UTC anchor (14 Oct) would give 21 Oct for today = 15 Oct.
    [InlineData(ContributionFrequency.Weekly, "2026-10-15", "2026-10-15")]
    [InlineData(ContributionFrequency.Weekly, "2026-10-16", "2026-10-22")]
    [InlineData(ContributionFrequency.BiWeekly, "2026-10-16", "2026-10-29")]
    [InlineData(ContributionFrequency.Monthly, "2026-10-16", "2026-11-15")]
    public void The_anchor_is_the_South_African_creation_date_not_the_UTC_date(
        ContributionFrequency frequency, string today, string expected)
    {
        var createdJustAfterSouthAfricanMidnight = new DateTimeOffset(2026, 10, 14, 22, 30, 0, TimeSpan.Zero);

        var next = MemberHomeService.NextPayoutDate(frequency, createdJustAfterSouthAfricanMidnight, DateOnly.Parse(today));

        Assert.Equal(DateOnly.Parse(expected), next);
    }

    [Theory]
    [InlineData(100.50, 10050)]
    [InlineData(0.1, 10)]
    [InlineData(399.995, 40000)]   // a stray third decimal rounds to even
    public void Amounts_become_exact_integer_cents(decimal amount, long cents) =>
        Assert.Equal(cents, MemberHomeService.ToCents(amount));
}
