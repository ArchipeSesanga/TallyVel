using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class HomeScreenTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Shows_each_stokvel_with_the_callers_own_role_and_amounts()
    {
        var me = await TestApi.RegisterAndLoginAsync(factory);
        var other = await TestApi.RegisterAndLoginAsync(factory);

        // S1: I created it, so I am Chairperson. Contribution amount 500.
        var s1 = await TestApi.CreateStokvelAsync(factory, me);
        var mine = TestApi.Client(factory, me.AccessToken);
        (await mine.PostAsJsonAsync($"/api/v1/stokvels/{s1}/cycles", new { label = "2026-10" })).EnsureSuccessStatusCode();
        var contribution = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{s1}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = me.Id, cycle = "2026-10", amountCents = 10050 }),
        };
        contribution.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await mine.SendAsync(contribution)).EnsureSuccessStatusCode();

        // S2: someone else's stokvel, where I am a plain Member.
        var s2 = await TestApi.CreateStokvelAsync(factory, other);
        (await TestApi.Client(factory, other.AccessToken)
            .PostAsJsonAsync($"/api/v1/stokvels/{s2}/members", new { userId = me.Id, role = "Member" }))
            .EnsureSuccessStatusCode();

        var response = await mine.GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stokvels")
            .EnumerateArray().ToDictionary(c => c.GetProperty("stokvelId").GetGuid());
        Assert.Equal(2, cards.Count);

        var chair = cards[s1];
        Assert.Equal("Chairperson", chair.GetProperty("role").GetString());
        var cycle = chair.GetProperty("currentCycle");
        Assert.Equal("2026-10", cycle.GetProperty("label").GetString());
        Assert.Equal("Open", cycle.GetProperty("status").GetString());
        Assert.Equal(10050, chair.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(39950, chair.GetProperty("owedThisCycleCents").GetInt64());
        Assert.Equal(JsonValueKind.String, chair.GetProperty("nextPayoutDate").ValueKind);

        var member = cards[s2];
        Assert.Equal("Member", member.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, member.GetProperty("currentCycle").ValueKind);
        Assert.Equal(0, member.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(0, member.GetProperty("owedThisCycleCents").GetInt64());
        Assert.Equal(JsonValueKind.Null, member.GetProperty("nextPayoutDate").ValueKind);
    }

    [Fact]
    public async Task A_user_in_no_stokvels_gets_an_empty_list()
    {
        var loner = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, loner.AccessToken).GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("stokvels").GetArrayLength());
    }
}
