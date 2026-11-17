using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tests.TestSupport;

namespace Tests.Frontend;

/// <summary>
/// GET /api/v1/stokvels/{id}/cycles/{cycleId}/payouts: who gets paid in what order, decided by the server so a client
/// never works out "whose turn is next" itself.
/// The order is the order members joined (ties broken by user id). The next member is the first one in that order
/// who has not been paid out yet.
/// </summary>
[Collection("Postgres collection")]
public class CyclePayoutsTests(PostgresApiFactory factory)
{
    private HttpClient As(TestUser user) => TestApi.Client(factory, user.AccessToken);

    private sealed record Rotation(TestUser Chair, TestUser Second, TestUser Third, Guid StokvelId, Guid CycleId);

    /// <summary>A stokvel whose members joined in the order Chair, Second, Third, with an open cycle holding R100.</summary>
    private async Task<Rotation> RotationAsync(string cycleLabel = "2026-10")
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var second = await TestApi.RegisterAndLoginAsync(factory);
        var third = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, chair);

        foreach (var member in new[] { second, third })
        {
            (await As(chair).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId = member.Id, role = "Member" }))
                .EnsureSuccessStatusCode();
            await Task.Delay(20); // joined-at must differ, so the order is the order they were added
        }

        var cycleId = await OpenCycleAsync(As(chair), stokvelId, cycleLabel);
        return new Rotation(chair, second, third, stokvelId, cycleId);
    }

    private static async Task<Guid> OpenCycleAsync(HttpClient client, Guid stokvelId, string label)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task RecordAsync(HttpClient client, Guid stokvelId, Guid memberId, string cycle, long cents)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = memberId, cycle, amountCents = cents }),
            Headers = { { "Idempotency-Key", Guid.NewGuid().ToString() } },
        };
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
    }

    private static async Task PayOutAsync(HttpClient client, Guid stokvelId, Guid cycleId, Guid recipientId) =>
        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", new { recipientUserId = recipientId }))
            .EnsureSuccessStatusCode();

    private async Task<JsonElement> GetAsync(TestUser who, Rotation r)
    {
        var response = await As(who).GetAsync($"/api/v1/stokvels/{r.StokvelId}/cycles/{r.CycleId}/payouts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<JsonElement> Entries(JsonElement body) => body.GetProperty("entries").EnumerateArray().ToList();

    [Fact]
    public async Task Before_anyone_is_paid_the_first_member_in_the_order_is_next()
    {
        var r = await RotationAsync();
        await RecordAsync(As(r.Chair), r.StokvelId, r.Chair.Id, "2026-10", 10000);

        var body = await GetAsync(r.Chair, r);
        var entries = Entries(body);

        Assert.Equal(3, entries.Count);
        Assert.Equal([r.Chair.Id, r.Second.Id, r.Third.Id], entries.Select(e => e.GetProperty("memberUserId").GetGuid()));
        Assert.Equal([1, 2, 3], entries.Select(e => e.GetProperty("position").GetInt32()));
        Assert.Equal(["Next", "Upcoming", "Upcoming"], entries.Select(e => e.GetProperty("status").GetString()));
        Assert.Equal([true, false, false], entries.Select(e => e.GetProperty("isNext").GetBoolean()));
        Assert.Equal(r.Chair.Id, body.GetProperty("nextMemberId").GetGuid());
        Assert.All(entries, e =>
        {
            Assert.Equal("Leak Test", e.GetProperty("fullName").GetString());
            Assert.Equal(JsonValueKind.Null, e.GetProperty("amountCents").ValueKind);
            Assert.Equal(JsonValueKind.Null, e.GetProperty("paidAt").ValueKind);
            Assert.False(e.GetProperty("isForThisCycle").GetBoolean());
        });
        Assert.Equal(r.CycleId, body.GetProperty("cycleId").GetGuid());
        Assert.Equal("2026-10", body.GetProperty("cycleLabel").GetString());
        Assert.Equal("Open", body.GetProperty("cycleStatus").GetString());
        Assert.Equal(10000, body.GetProperty("potCents").GetInt64()); // what a payout of this cycle would be
    }

    [Fact]
    public async Task After_a_payout_that_member_is_PaidOut_with_the_amount_and_date_and_the_next_one_moves_on()
    {
        var r = await RotationAsync();
        await RecordAsync(As(r.Chair), r.StokvelId, r.Chair.Id, "2026-10", 12345);
        await PayOutAsync(As(r.Chair), r.StokvelId, r.CycleId, r.Chair.Id);

        var body = await GetAsync(r.Second, r); // a plain Member may read it
        var entries = Entries(body);

        Assert.Equal(["PaidOut", "Next", "Upcoming"], entries.Select(e => e.GetProperty("status").GetString()));
        Assert.Equal(r.Second.Id, body.GetProperty("nextMemberId").GetGuid());
        var paid = entries[0];
        Assert.Equal(12345, paid.GetProperty("amountCents").GetInt64());
        Assert.True(paid.GetProperty("isForThisCycle").GetBoolean());
        Assert.EndsWith("Z", paid.GetProperty("paidAt").GetString()); // a UTC instant, like every other timestamp
        Assert.Equal("PaidOut", body.GetProperty("cycleStatus").GetString());
    }

    [Fact]
    public async Task Next_is_the_first_member_in_the_order_who_has_not_been_paid_even_if_a_later_one_was_paid_first()
    {
        var r = await RotationAsync();
        await RecordAsync(As(r.Chair), r.StokvelId, r.Chair.Id, "2026-10", 5000);
        await PayOutAsync(As(r.Chair), r.StokvelId, r.CycleId, r.Third.Id); // the treasurer paid position 3 out of turn

        var body = await GetAsync(r.Chair, r);
        var entries = Entries(body);

        Assert.Equal(["Next", "Upcoming", "PaidOut"], entries.Select(e => e.GetProperty("status").GetString()));
        Assert.Equal(r.Chair.Id, body.GetProperty("nextMemberId").GetGuid());
    }

    [Fact]
    public async Task Once_everyone_has_been_paid_nobody_is_next()
    {
        var r = await RotationAsync();
        var cycles = new[] { ("2026-10", r.CycleId), ("2026-11", Guid.Empty), ("2026-12", Guid.Empty) };
        var recipients = new[] { r.Chair, r.Second, r.Third };
        for (var i = 0; i < 3; i++)
        {
            var (label, id) = cycles[i];
            var cycleId = id == Guid.Empty ? await OpenCycleAsync(As(r.Chair), r.StokvelId, label) : id;
            await RecordAsync(As(r.Chair), r.StokvelId, r.Chair.Id, label, 1000 + i);
            await PayOutAsync(As(r.Chair), r.StokvelId, cycleId, recipients[i].Id);
        }

        var body = await GetAsync(r.Chair, r);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("nextMemberId").ValueKind);
        Assert.All(Entries(body), e =>
        {
            Assert.Equal("PaidOut", e.GetProperty("status").GetString());
            Assert.False(e.GetProperty("isNext").GetBoolean());
        });
    }

    [Fact]
    public async Task A_non_member_gets_404_and_cannot_tell_the_stokvel_exists()
    {
        var r = await RotationAsync();
        var outsider = await TestApi.RegisterAndLoginAsync(factory);

        var response = await As(outsider).GetAsync($"/api/v1/stokvels/{r.StokvelId}/cycles/{r.CycleId}/payouts");
        var unknownStokvel = await As(outsider).GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}/cycles/{r.CycleId}/payouts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownStokvel.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("correlationId").GetString()));
        Assert.DoesNotContain(r.Chair.Id.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_cycle_of_another_stokvel_is_404_with_a_code()
    {
        var r = await RotationAsync();
        var other = await TestApi.RegisterAndLoginAsync(factory);
        var otherStokvel = await TestApi.CreateStokvelAsync(factory, other);
        var otherCycle = await OpenCycleAsync(As(other), otherStokvel, "2026-10");

        var response = await As(r.Chair).GetAsync($"/api/v1/stokvels/{r.StokvelId}/cycles/{otherCycle}/payouts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cycle-not-found", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Signing_in_is_required()
    {
        var r = await RotationAsync();

        var response = await TestApi.Client(factory)
            .GetAsync($"/api/v1/stokvels/{r.StokvelId}/cycles/{r.CycleId}/payouts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_document_describes_the_endpoint_and_its_statuses()
    {
        var doc = JsonNode.Parse(await TestApi.Client(factory).GetStringAsync("/openapi/v1.json"))!;

        var operation = doc["paths"]!["/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts"]!["get"]!;
        Assert.Equal(["200", "404"], operation["responses"]!.AsObject().Select(p => p.Key).Order());

        var statuses = doc["components"]!["schemas"]!["PayoutEntryStatus"]!["enum"]!.AsArray().Select(s => s!.GetValue<string>());
        Assert.Equal(["PaidOut", "Next", "Upcoming"], statuses);

        var entry = doc["components"]!["schemas"]!["PayoutEntry"]!;
        Assert.Equal(
            ["memberUserId", "fullName", "position", "amountCents", "paidAt", "status", "isNext", "isForThisCycle"],
            entry["required"]!.AsArray().Select(p => p!.GetValue<string>()));
    }
}
