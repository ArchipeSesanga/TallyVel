using Domain;
using Microsoft.Extensions.Time.Testing;
using Tests.TestSupport;

namespace Tests.Jobs;

[Collection("Postgres collection")]
public class CycleCloseJobTests(PostgresApiFactory factory)
{
    private readonly JobTestData _data = new(factory);

    private static FakeTimeProvider ClockAt(DateTimeOffset now)
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(now);
        return clock;
    }

    private static DateOnly Today => JobTestData.RealToday();

    [Fact]
    public async Task An_ended_cycle_is_closed_with_arrears_and_the_next_cycle_opens()
    {
        var end = Today.AddDays(6);
        var s = await _data.SeedAsync(3, 500m, ContributionFrequency.Weekly, Today, end,
            new Dictionary<int, decimal> { [0] = 500m, [1] = 200m });

        await factory.CreateCloser(ClockAt(StokvelCalendar.ClosesAt(end))).RunOnceAsync(CancellationToken.None);

        Assert.NotNull(await _data.ClosedAtAsync(s.CycleId));
        var arrears = await _data.ArrearsAsync(s.StokvelId);
        Assert.Equal(2, arrears.Count);
        Assert.Equal(30000, arrears.Single(a => a.MemberUserId == s.UserIds[1]).ShortfallCents);
        Assert.Equal(50000, arrears.Single(a => a.MemberUserId == s.UserIds[2]).ShortfallCents);
        Assert.DoesNotContain(arrears, a => a.MemberUserId == s.UserIds[0]);
        Assert.Contains(await _data.CyclesAsync(s.StokvelId), c => c.StartsOn == end.AddDays(1));
    }

    [Fact]
    public async Task A_cycle_ending_tomorrow_is_left_alone()
    {
        var s = await _data.SeedAsync(2, 500m, ContributionFrequency.Weekly, Today, Today.AddDays(1));

        await factory.CreateCloser(TimeProvider.System).RunOnceAsync(CancellationToken.None);

        Assert.Null(await _data.ClosedAtAsync(s.CycleId));
        Assert.Empty(await _data.ArrearsAsync(s.StokvelId));
        Assert.Single(await _data.CyclesAsync(s.StokvelId));
    }

    [Fact]
    public async Task A_cycle_closes_at_midnight_South_African_time_not_a_minute_before()
    {
        var end = Today.AddDays(5);
        var s = await _data.SeedAsync(2, 500m, ContributionFrequency.Weekly, Today, end);
        var closesAt = StokvelCalendar.ClosesAt(end);   // end 22:00Z

        await factory.CreateCloser(ClockAt(closesAt.AddMinutes(-1))).RunOnceAsync(CancellationToken.None);
        Assert.Null(await _data.ClosedAtAsync(s.CycleId));

        await factory.CreateCloser(ClockAt(closesAt)).RunOnceAsync(CancellationToken.None);
        Assert.NotNull(await _data.ClosedAtAsync(s.CycleId));
    }

    [Fact]
    public async Task Running_twice_changes_nothing_the_second_time()
    {
        var end = Today.AddDays(6);
        var s = await _data.SeedAsync(3, 500m, ContributionFrequency.Weekly, Today, end,
            new Dictionary<int, decimal> { [0] = 500m });
        var clock = ClockAt(StokvelCalendar.ClosesAt(end));

        await factory.CreateCloser(clock).RunOnceAsync(CancellationToken.None);
        var cycles1 = await _data.CyclesAsync(s.StokvelId);
        var arrears1 = await _data.ArrearsAsync(s.StokvelId);

        await factory.CreateCloser(clock).RunOnceAsync(CancellationToken.None);

        Assert.Equal(cycles1, await _data.CyclesAsync(s.StokvelId));
        Assert.Equal(arrears1, await _data.ArrearsAsync(s.StokvelId));
    }

    [Fact]
    public async Task Two_closers_racing_close_the_cycle_once()
    {
        var end = Today.AddDays(6);
        var s = await _data.SeedAsync(3, 500m, ContributionFrequency.Weekly, Today, end,
            new Dictionary<int, decimal> { [0] = 500m });
        var clock = ClockAt(StokvelCalendar.ClosesAt(end));
        var a = factory.CreateCloser(clock);
        var b = factory.CreateCloser(clock);

        await Task.WhenAll(a.RunOnceAsync(CancellationToken.None), b.RunOnceAsync(CancellationToken.None));

        var cycles = await _data.CyclesAsync(s.StokvelId);
        Assert.Equal(2, cycles.Count);                              // original + exactly one next
        Assert.NotNull(cycles.Single(c => c.Id == s.CycleId).ClosedAt);
        var arrears = await _data.ArrearsAsync(s.StokvelId);
        Assert.Equal(2, arrears.Count);                             // one per underpaying member
        Assert.Equal(2, arrears.Select(x => x.MemberUserId).Distinct().Count());
    }

    [Fact]
    public async Task Missed_runs_are_caught_up_and_one_current_cycle_is_left_open()
    {
        var start = Today;
        var s = await _data.SeedAsync(2, 500m, ContributionFrequency.Weekly, start, start.AddDays(6),
            new Dictionary<int, decimal> { [0] = 500m });
        var clockDay = start.AddDays(27);
        var clock = ClockAt(StokvelCalendar.ClosesAt(clockDay.AddDays(-1)));   // 00:00 SAST on clockDay

        await factory.CreateCloser(clock).RunOnceAsync(CancellationToken.None);

        var cycles = await _data.CyclesAsync(s.StokvelId);
        var closed = cycles.Where(c => c.ClosedAt != null).ToList();
        Assert.Equal(new[] { start.AddDays(6), start.AddDays(13), start.AddDays(20) }, closed.Select(c => c.EndsOn!.Value));
        var arrears = await _data.ArrearsAsync(s.StokvelId);
        foreach (var c in closed)
            Assert.Contains(arrears, a => a.CycleId == c.Id && a.MemberUserId == s.UserIds[1]);
        var open = Assert.Single(cycles, c => c.ClosedAt == null);
        Assert.True(open.StartsOn <= clockDay && clockDay <= open.EndsOn);

        await factory.CreateCloser(clock).RunOnceAsync(CancellationToken.None);
        Assert.Equal(cycles, await _data.CyclesAsync(s.StokvelId));
        Assert.Equal(arrears, await _data.ArrearsAsync(s.StokvelId));
    }

    [Fact]
    public async Task One_stokvel_failing_rolls_back_alone_and_does_not_stop_the_others()
    {
        var end = Today.AddDays(6);
        var good = await _data.SeedAsync(2, 500m, ContributionFrequency.Weekly, Today, end);
        var bad = await _data.SeedAsync(2, 500m, ContributionFrequency.Weekly, Today, end);
        // Impossible state: the arrear the job is about to write already exists -> unique violation.
        await _data.AddArrearAsync(bad, 1, 1);

        var result = await factory.CreateCloser(ClockAt(StokvelCalendar.ClosesAt(end)))
            .RunOnceAsync(CancellationToken.None);

        Assert.NotNull(await _data.ClosedAtAsync(good.CycleId));
        Assert.Null(await _data.ClosedAtAsync(bad.CycleId));
        Assert.Single(await _data.CyclesAsync(bad.StokvelId));      // no next cycle either
        Assert.True(result.Failed >= 1);
    }
}
