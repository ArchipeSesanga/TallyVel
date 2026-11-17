using System.Threading.RateLimiting;

namespace Api.Common;

/// <summary>
/// Wraps a fixed-window or token-bucket limiter so that a refusal says how long is LEFT until the limiter refills.
///
/// Why: the built-in limiters put the whole window (times the number of periods needed) in the refusal's RetryAfter
/// metadata. They do not subtract the time that has already passed, so a caller 55 seconds into a 60-second window
/// is still told "60". Both limiters refill on a timer that starts when the limiter is created, so the next refill is
/// always at <c>start + k x period</c>; this class remembers <c>start</c> and does that sum.
/// </summary>
public sealed class RemainingTimeLimiter : RateLimiter
{
    private readonly RateLimiter _inner;
    private readonly TimeSpan _period;
    private readonly TimeProvider _clock;
    private readonly long _start;

    /// <param name="inner">A limiter whose refill timer starts now and fires every <paramref name="period"/>.</param>
    public RemainingTimeLimiter(RateLimiter inner, TimeSpan period, TimeProvider clock)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period), "The refill period must be positive.");

        _inner = inner;
        _period = period;
        _clock = clock;
        _start = clock.GetTimestamp();
    }

    /// <summary>Time until the next refill. Always more than zero and at most one period.</summary>
    public TimeSpan UntilNextRefill()
    {
        var intoPeriod = TimeSpan.FromTicks(_clock.GetElapsedTime(_start).Ticks % _period.Ticks);
        return _period - intoPeriod;
    }

    public override RateLimiterStatistics? GetStatistics() => _inner.GetStatistics();

    public override TimeSpan? IdleDuration => _inner.IdleDuration;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Report(_inner.AttemptAcquire(permitCount));

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        Report(await _inner.AcquireAsync(permitCount, cancellationToken));

    private RateLimitLease Report(RateLimitLease lease)
    {
        if (lease.IsAcquired)
            return lease;

        lease.Dispose();
        return new RefusedLease(UntilNextRefill());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore() => _inner.DisposeAsync();

    private sealed class RefusedLease(TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
