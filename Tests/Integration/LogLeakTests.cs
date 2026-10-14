using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tests.TestSupport;

namespace Tests.Integration;

[Collection("Postgres collection")]
public class LogLeakTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task No_personal_or_secret_data_reaches_the_logs()
    {
        factory.Logs.Clear();

        // register + log in
        var user = await TestApi.RegisterAndLoginAsync(factory);

        // refresh
        var anon = TestApi.Client(factory);
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"tallyvel_rt={user.RefreshToken}");
        var refresh = await anon.SendAsync(refreshRequest);
        refresh.EnsureSuccessStatusCode();
        var newRefreshToken = TestApi.RefreshCookie(refresh);
        var newAccessToken = (await refresh.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;

        // record a contribution with a distinctive amount
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = TestApi.Client(factory, newAccessToken);
        var contribution = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = user.Id, cycle = "2026-10", amount = 1234.56m })
        };
        contribution.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(contribution)).EnsureSuccessStatusCode();

        // a duplicate registration -> 409, and no Error entry for it
        var errorsBefore = factory.Logs.Entries.Count(e => e.Level >= LogLevel.Error);
        var duplicate = await anon.PostAsJsonAsync("/api/auth/register",
            new { email = user.Email.ToUpperInvariant(), password = user.Password, fullName = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(errorsBefore, factory.Logs.Entries.Count(e => e.Level >= LogLevel.Error));

        string[] secrets =
        [
            user.Email, user.Email.ToUpperInvariant(), user.Password,
            user.AccessToken, user.RefreshToken, newAccessToken, newRefreshToken,
            "1234.56", "1234,56",
        ];
        foreach (var entry in factory.Logs.Entries)
            foreach (var secret in secrets)
                Assert.False(entry.AllText.Contains(secret, StringComparison.OrdinalIgnoreCase),
                    $"[{entry.Category}] leaked a secret: {entry.Message}");
    }
}