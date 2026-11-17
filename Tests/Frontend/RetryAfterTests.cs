using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Tests.TestSupport;

namespace Tests.Frontend;

/// <summary>
/// A 429's Retry-After must be the time LEFT until the limiter lets the caller in again, not the length of the
/// whole window. The built-in limiters report the window length every time (the lease's RetryAfter metadata is
/// window x periods, with no account of how much has elapsed), so a client that obeyed it would always wait too long
/// and a "try again in N seconds" label would never count down.
/// </summary>
[Collection("Postgres collection")]
public class RetryAfterTests(PostgresApiFactory factory)
{
    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
                b.UseSetting(key, value);
        });

    private static double Seconds(HttpResponseMessage response)
    {
        Assert.True(response.Headers.RetryAfter?.Delta is not null, "Retry-After is missing or not a number of seconds");
        return response.Headers.RetryAfter!.Delta!.Value.TotalSeconds;
    }

    private static HttpRequestMessage Contribution(Guid stokvelId, Guid memberId, string cycle) =>
        new(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = memberId, cycle, amountCents = 10000 }),
            Headers = { { "Idempotency-Key", Guid.NewGuid().ToString() } },
        };

    [Fact]
    public async Task A_login_429_counts_down_as_the_window_runs_out()
    {
        await using var app = WithSettings(("RateLimiting:Login:PermitLimit", "1"), ("RateLimiting:Login:WindowSeconds", "6"));
        var client = TestApi.Client(app);
        var bad = new { email = "nobody@retry-after.test", password = "not-the-password-123" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", bad)).StatusCode);

        var first = await client.PostAsJsonAsync("/api/v1/auth/login", bad);
        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        var waitAtFirst = Seconds(first);
        Assert.InRange(waitAtFirst, 1, 6);

        await Task.Delay(TimeSpan.FromSeconds(3));

        var second = await client.PostAsJsonAsync("/api/v1/auth/login", bad);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var waitAtSecond = Seconds(second);
        Assert.InRange(waitAtSecond, 1, 4);
        Assert.True(waitAtSecond <= waitAtFirst - 2,
            $"Retry-After did not count down: {waitAtFirst} s, then {waitAtSecond} s three seconds later");
    }

    [Fact]
    public async Task Waiting_the_advertised_time_is_enough()
    {
        await using var app = WithSettings(("RateLimiting:Login:PermitLimit", "1"), ("RateLimiting:Login:WindowSeconds", "4"));
        var client = TestApi.Client(app);
        var bad = new { email = "nobody@retry-after.test", password = "not-the-password-123" };

        await client.PostAsJsonAsync("/api/v1/auth/login", bad);
        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", bad);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(Seconds(limited)) + TimeSpan.FromMilliseconds(300));

        // Not limited any more: the (bad) login is answered with 401, not 429.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", bad)).StatusCode);
    }

    [Fact]
    public async Task A_money_429_counts_down_until_the_next_token()
    {
        await using var app = WithSettings(
            ("RateLimiting:Money:TokenLimit", "1"),
            ("RateLimiting:Money:TokensPerPeriod", "1"),
            ("RateLimiting:Money:ReplenishmentPeriodSeconds", "6"));
        var user = await TestApi.RegisterAndLoginAsync(app);
        var stokvelId = await TestApi.CreateStokvelAsync(app, user);
        var client = TestApi.Client(app, user.AccessToken);

        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(Contribution(stokvelId, user.Id, "2026-01"))).StatusCode);

        var first = await client.SendAsync(Contribution(stokvelId, user.Id, "2026-02"));
        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        var waitAtFirst = Seconds(first);
        Assert.InRange(waitAtFirst, 1, 6);

        await Task.Delay(TimeSpan.FromSeconds(3));

        var second = await client.SendAsync(Contribution(stokvelId, user.Id, "2026-02"));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(Seconds(second) <= waitAtFirst - 2,
            $"Retry-After did not count down: {waitAtFirst} s, then {Seconds(second)} s three seconds later");
    }
}
