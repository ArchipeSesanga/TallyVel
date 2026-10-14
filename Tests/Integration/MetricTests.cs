using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Integration;
[Collection("Postgres collection")]
public class MetricTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task A_payout_is_counted_with_its_outcome()
    {
        var meterFactory = factory.Services.GetRequiredService<IMeterFactory>();
        using var payouts = new MetricCollector<long>(meterFactory, "TallyVel", "tallyvel.payouts.processed");

        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = TestApi.Client(factory, user.AccessToken);

        var cycle = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label = "2026-10" });
        var cycleId = (await cycle.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var contribution = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = user.Id, cycle = "2026-10", amount = 500m })
        };
        contribution.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(contribution)).EnsureSuccessStatusCode();

        var payout = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", new { recipientUserId = user.Id });
        Assert.Equal(HttpStatusCode.Created, payout.StatusCode);

        Assert.Contains(payouts.GetMeasurementSnapshot(),
            m => m.Value == 1 && Equals(m.Tags["outcome"], "succeeded"));
    }
}