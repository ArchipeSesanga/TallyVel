using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Common;

/// <summary>
/// Rate-limit policy names, defined once. Controllers only reference them
/// through [EnableRateLimiting(RateLimitPolicies.X)].
/// </summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Money = "money";

    public static IServiceCollection AddTallyVelRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        // Login: a fixed window per client IP. There is no user yet, so the
        // IP is the only key; it slows password guessing from one machine.
        var loginPermitLimit = config.GetValue("RateLimiting:Login:PermitLimit", 5);
        var loginWindow = TimeSpan.FromSeconds(config.GetValue("RateLimiting:Login:WindowSeconds", 60));

        // Money (record contribution, process payout): a token bucket per
        // signed-in user. A treasurer records a burst after a meeting, then
        // nothing for weeks; the bucket allows the burst and refills slowly.
        var moneyTokenLimit = config.GetValue("RateLimiting:Money:TokenLimit", 30);
        var moneyTokensPerPeriod = config.GetValue("RateLimiting:Money:TokensPerPeriod", 10);
        var moneyPeriod = TimeSpan.FromSeconds(config.GetValue("RateLimiting:Money:ReplenishmentPeriodSeconds", 60));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(Login, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginPermitLimit,
                    Window = loginWindow,
                    QueueLimit = 0,
                }));

            options.AddPolicy(Money, http =>
            {
                // No "sub" means not signed in: let it through to authorization,
                // which answers with the normal 401.
                var userId = http.User.FindFirst("sub")?.Value;
                if (userId is null)
                    return RateLimitPartition.GetNoLimiter("anonymous");

                return RateLimitPartition.GetTokenBucketLimiter(userId, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = moneyTokenLimit,
                    TokensPerPeriod = moneyTokensPerPeriod,
                    ReplenishmentPeriod = moneyPeriod,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });
    }

    // Every rejection: 429, Retry-After in seconds, and problem+json written
    // through IProblemDetailsService so correlationId is added like any error.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        // POPIA: the user id (or "anonymous") and the policy, never the email or body.
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "unknown";
        http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Api.RateLimiting")
            .LogInformation("Rate limit {Policy} rejected a request from {UserId}",
                policy, http.User.FindFirst("sub")?.Value ?? "anonymous");

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too Many Requests",
                Detail = "Too many requests. Wait the number of seconds in the Retry-After header, then try again.",
            },
        });
    }
}
