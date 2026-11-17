using System.Threading.RateLimiting;
using Api.Common;
using Microsoft.Extensions.Time.Testing;

namespace Tests.Unit;

/// <summary>The sum behind Retry-After: how long until the limiter's next refill, on a clock the test controls.</summary>
public class RemainingTimeLimiterTests
{
    private static RemainingTimeLimiter LimiterOn(FakeTimeProvider clock, TimeSpan period) =>
        new(new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1, Window = period, QueueLimit = 0, AutoReplenishment = false,
        }), period, clock);

    [Theory]
    [InlineData(0, 60)]    // just started: the whole period
    [InlineData(1, 59)]
    [InlineData(55, 5)]    // nearly over: 5 seconds, not 60
    [InlineData(59, 1)]
    [InlineData(60, 60)]   // exactly one period gone: a new period has begun
    [InlineData(65, 55)]   // the sum repeats every period
    [InlineData(185, 55)]
    public void Time_until_the_next_refill_is_what_is_left_of_the_current_period(int secondsElapsed, int expectedSeconds)
    {
        var clock = new FakeTimeProvider();
        using var limiter = LimiterOn(clock, TimeSpan.FromSeconds(60));

        clock.Advance(TimeSpan.FromSeconds(secondsElapsed));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), limiter.UntilNextRefill());
    }

    [Fact]
    public void A_refusal_carries_the_time_left_as_its_RetryAfter_metadata()
    {
        var clock = new FakeTimeProvider();
        using var limiter = LimiterOn(clock, TimeSpan.FromSeconds(60));
        using var first = limiter.AttemptAcquire();
        Assert.True(first.IsAcquired);

        clock.Advance(TimeSpan.FromSeconds(42));
        using var refused = limiter.AttemptAcquire();

        Assert.False(refused.IsAcquired);
        Assert.True(refused.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(18), retryAfter);
    }

    [Fact]
    public void A_granted_request_is_passed_through_untouched()
    {
        using var limiter = LimiterOn(new FakeTimeProvider(), TimeSpan.FromSeconds(60));

        using var lease = limiter.AttemptAcquire();

        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public void A_period_of_zero_is_refused()
    {
        var inner = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1, Window = TimeSpan.FromSeconds(1), QueueLimit = 0, AutoReplenishment = false,
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new RemainingTimeLimiter(inner, TimeSpan.Zero, TimeProvider.System));
    }
}
