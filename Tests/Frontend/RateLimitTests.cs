using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;

namespace Tests.Frontend;

/// <summary>
/// Each test builds its own host with tiny limits (WithWebHostBuilder), so it
/// never uses up the shared fixture's allowance. Same database container.
/// </summary>
[Collection("Postgres collection")]
public class RateLimitTests(PostgresApiFactory factory)
{
    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
                b.UseSetting(key, value);
        });

    private static HttpRequestMessage Contribution(Guid stokvelId, Guid memberId, string cycle, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = memberId, cycle, amount = 100m }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    // Retries while the limiter says 429, so the test waits out the
    // replenishment period without a fixed sleep.
    private static async Task<HttpResponseMessage> SendUntilNotLimitedAsync(HttpClient client, Func<HttpRequestMessage> build)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var response = await client.SendAsync(build());
            if (response.StatusCode != HttpStatusCode.TooManyRequests || DateTime.UtcNow > deadline)
                return response;
            await Task.Delay(250);
        }
    }

    [Fact]
    public async Task The_third_bad_login_is_a_429_problem_with_retry_after()
    {
        await using var app = WithSettings(("RateLimiting:Login:PermitLimit", "2"), ("RateLimiting:Login:WindowSeconds", "60"));
        var client = TestApi.Client(app);
        var bad = new { email = "nobody@rate-limit.test", password = "not-the-password-123" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", bad)).StatusCode);
        var third = await client.PostAsJsonAsync("/api/v1/auth/login", bad);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
        Assert.True(third.Headers.RetryAfter is not null, "Retry-After header missing");
        var body = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Too Many Requests", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task A_rate_limited_contribution_does_not_use_up_its_idempotency_key()
    {
        await using var app = WithSettings(
            ("RateLimiting:Money:TokenLimit", "1"),
            ("RateLimiting:Money:TokensPerPeriod", "1"),
            ("RateLimiting:Money:ReplenishmentPeriodSeconds", "2"));
        var user = await TestApi.RegisterAndLoginAsync(app);
        var stokvelId = await TestApi.CreateStokvelAsync(app, user);
        var client = TestApi.Client(app, user.AccessToken);

        // Use up the bucket.
        Assert.Equal(HttpStatusCode.Created,
            (await client.SendAsync(Contribution(stokvelId, user.Id, "2026-01", Guid.NewGuid().ToString()))).StatusCode);

        // Key K is rejected by the limiter, before ContributionServices runs.
        var key = Guid.NewGuid().ToString();
        var limited = await client.SendAsync(Contribution(stokvelId, user.Id, "2026-02", key));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        // Once the bucket refills, K is still unused: 201, not 409 "in progress".
        var first = await SendUntilNotLimitedAsync(client, () => Contribution(stokvelId, user.Id, "2026-02", key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();

        // Sending K again is a replay of that success.
        var replay = await SendUntilNotLimitedAsync(client, () => Contribution(stokvelId, user.Id, "2026-02", key));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(firstBody, await replay.Content.ReadAsStringAsync());

        await using var db = factory.CreateContext();
        Assert.Equal(1, await db.Contributions.CountAsync(c => c.MemberUserId == user.Id && c.Cycle == "2026-02"));
    }

    [Fact]
    public async Task One_user_hitting_the_money_limit_does_not_block_another()
    {
        await using var app = WithSettings(
            ("RateLimiting:Money:TokenLimit", "1"),
            ("RateLimiting:Money:TokensPerPeriod", "1"),
            ("RateLimiting:Money:ReplenishmentPeriodSeconds", "600"));
        var a = await TestApi.RegisterAndLoginAsync(app);
        var b = await TestApi.RegisterAndLoginAsync(app);
        var stokvelA = await TestApi.CreateStokvelAsync(app, a);
        var stokvelB = await TestApi.CreateStokvelAsync(app, b);
        var clientA = TestApi.Client(app, a.AccessToken);

        Assert.Equal(HttpStatusCode.Created,
            (await clientA.SendAsync(Contribution(stokvelA, a.Id, "2026-01", Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await clientA.SendAsync(Contribution(stokvelA, a.Id, "2026-02", Guid.NewGuid().ToString()))).StatusCode);

        var forB = await TestApi.Client(app, b.AccessToken)
            .SendAsync(Contribution(stokvelB, b.Id, "2026-01", Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Created, forB.StatusCode);
    }
}
