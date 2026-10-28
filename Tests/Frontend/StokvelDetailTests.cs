using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class StokvelDetailTests(PostgresApiFactory factory)
{
    private static async Task RecordAsync(HttpClient client, Guid stokvelId, Guid memberId, string cycle, long cents)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = memberId, cycle, amountCents = cents }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_stokvel_with_an_open_cycle_shows_member_count_and_cents_collected_and_due()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var member = await TestApi.RegisterAndLoginAsync(factory);
        var stokvel = await TestApi.CreateStokvelAsync(factory, chair); // R500 a member
        var client = TestApi.Client(factory, chair.AccessToken);
        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvel}/members", new { userId = member.Id, role = "Member" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvel}/cycles", new { label = "2026-10" }))
            .EnsureSuccessStatusCode();
        await RecordAsync(client, stokvel, chair.Id, "2026-10", 50000);
        await RecordAsync(client, stokvel, member.Id, "2026-10", 12050);
        await RecordAsync(client, stokvel, member.Id, "2026-09", 99999); // another cycle: must not count

        var response = await client.GetAsync($"/api/v1/stokvels/{stokvel}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("memberCount").GetInt32());
        var cycle = body.GetProperty("currentCycle");
        Assert.Equal("2026-10", cycle.GetProperty("label").GetString());
        Assert.Equal("Open", cycle.GetProperty("status").GetString());
        Assert.True(cycle.TryGetProperty("endsOn", out _));
        Assert.NotEqual(Guid.Empty, cycle.GetProperty("id").GetGuid());
        Assert.Equal(62050, cycle.GetProperty("collectedCents").GetInt64());
        Assert.Equal(100000, cycle.GetProperty("dueCents").GetInt64());
    }

    [Fact]
    public async Task A_stokvel_without_an_open_cycle_has_a_null_current_cycle()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var stokvel = await TestApi.CreateStokvelAsync(factory, chair);

        var response = await TestApi.Client(factory, chair.AccessToken).GetAsync($"/api/v1/stokvels/{stokvel}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("memberCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentCycle").ValueKind);
    }

    [Fact]
    public async Task A_non_member_and_a_missing_id_both_get_404()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var outsider = await TestApi.RegisterAndLoginAsync(factory);
        var stokvel = await TestApi.CreateStokvelAsync(factory, chair);
        var client = TestApi.Client(factory, outsider.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/stokvels/{stokvel}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}")).StatusCode);
    }
}
