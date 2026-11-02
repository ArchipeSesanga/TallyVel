using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>GET /api/v1/me/activity: the caller's feed across only their own stokvels.</summary>
[Collection("Postgres collection")]
public class ActivityFeedTests(PostgresApiFactory factory)
{
    private async Task InsertContributionAsync(Guid stokvelId, Guid memberId, string cycle, DateTimeOffset recordedAt)
    {
        await using var db = factory.CreateContext();
        // A contribution points at its cycle (a foreign key), so the cycle must exist first.
        if (!db.ContributionCycles.Any(c => c.StokvelId == stokvelId && c.Label == cycle))
            db.ContributionCycles.Add(new ContributionCycle(stokvelId, cycle));
        db.Contributions.Add(new Contribution(stokvelId, memberId, cycle, 100m, ContributionStatus.Paid, recordedAt));
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> FeedAsync(TestUser caller, string query = "")
    {
        var response = await TestApi.Client(factory, caller.AccessToken).GetAsync($"/api/v1/me/activity{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_feed_contains_only_my_stokvels_and_nothing_from_anyone_elses()
    {
        var me = await TestApi.RegisterAndLoginAsync(factory);
        var stranger = await TestApi.RegisterAndLoginAsync(factory);
        var mine = await TestApi.CreateStokvelAsync(factory, me);
        var theirs = await TestApi.CreateStokvelAsync(factory, stranger);
        await InsertContributionAsync(mine, me.Id, "mine-1", DateTimeOffset.UtcNow.AddMinutes(-5));
        await InsertContributionAsync(theirs, stranger.Id, "theirs-1", DateTimeOffset.UtcNow.AddMinutes(-4));

        var feed = await FeedAsync(me, "?pageSize=100");

        var items = feed.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(mine, i.GetProperty("stokvelId").GetGuid()));
        Assert.DoesNotContain(items, i => i.ToString().Contains(theirs.ToString()));
    }

    [Fact]
    public async Task The_feed_is_newest_first_and_describes_each_kind_of_event()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var member = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, chair);
        (await TestApi.Client(factory, chair.AccessToken).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members",
            new { userId = member.Id, role = "Member" })).EnsureSuccessStatusCode();
        var older = DateTimeOffset.UtcNow.AddHours(1);
        await InsertContributionAsync(stokvelId, member.Id, "older", older);
        await InsertContributionAsync(stokvelId, member.Id, "newer", older.AddHours(1));

        var items = (await FeedAsync(chair, "?pageSize=100")).GetProperty("items").EnumerateArray().ToList();

        var times = items.Select(i => i.GetProperty("occurredAt").GetDateTimeOffset()).ToList();
        Assert.Equal(times.OrderByDescending(t => t).ToList(), times); // newest first
        Assert.All(items, i => Assert.EndsWith("Z", i.GetProperty("occurredAt").GetString()));

        var contribution = items.First(i => i.GetProperty("kind").GetString() == "ContributionRecorded");
        Assert.Equal(10000, contribution.GetProperty("amountCents").GetInt64()); // 100 rands, in cents
        Assert.Equal("Leak Test", contribution.GetProperty("personName").GetString());
        Assert.Equal(stokvelId, contribution.GetProperty("stokvelId").GetGuid());
        Assert.Equal("Leak Test Club", contribution.GetProperty("stokvelName").GetString());

        var joined = items.Where(i => i.GetProperty("kind").GetString() == "MemberJoined").ToList();
        Assert.Equal(2, joined.Count); // the chairperson who created it, and the member added
        Assert.All(joined, j => Assert.Equal(JsonValueKind.Null, j.GetProperty("amountCents").ValueKind));
    }

    [Fact]
    public async Task A_payout_appears_in_the_feed_with_its_amount()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, chair);
        var client = TestApi.Client(factory, chair.AccessToken);
        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label = "2026-11" })).EnsureSuccessStatusCode();
        await using (var db = factory.CreateContext())
        {
            var cycle = db.ContributionCycles.Single(c => c.StokvelId == stokvelId);
            db.Payouts.Add(new Payout(stokvelId, cycle.Id, chair.Id, 250m));
            await db.SaveChangesAsync();
        }

        var items = (await FeedAsync(chair, "?pageSize=100")).GetProperty("items").EnumerateArray().ToList();

        var payout = items.Single(i => i.GetProperty("kind").GetString() == "PayoutMade");
        Assert.Equal(25000, payout.GetProperty("amountCents").GetInt64());
    }

    [Fact]
    public async Task Paging_through_the_feed_never_repeats_or_skips_a_row_and_ends_with_an_empty_token()
    {
        var me = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, me);
        var start = DateTimeOffset.UtcNow.AddHours(2);
        // Seven rows, two of them with exactly the same time: the tie-break must keep the order stable.
        for (var i = 0; i < 7; i++)
            await InsertContributionAsync(stokvelId, me.Id, $"c{i}", start.AddMinutes(i == 6 ? 5 : i));

        var all = (await FeedAsync(me, "?pageSize=100")).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()!).ToList();

        var paged = new List<string>();
        string? token = null;
        var pages = 0;
        do
        {
            var query = token is null ? "?pageSize=3" : $"?pageSize=3&pageToken={Uri.EscapeDataString(token)}";
            var page = await FeedAsync(me, query);
            paged.AddRange(page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!));
            token = page.GetProperty("nextPageToken").GetString();
            pages++;
        } while (!string.IsNullOrEmpty(token));

        Assert.Equal(all, paged);                     // same rows, same order as one big page
        Assert.Equal(paged.Count, paged.Distinct().Count()); // nothing repeated
        Assert.True(pages > 1);
        Assert.Equal("", token);                      // the last page's token is the empty string
    }

    [Fact]
    public async Task A_malformed_page_token_gets_400()
    {
        var me = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, me.AccessToken).GetAsync("/api/v1/me/activity?pageToken=not-a-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_feed_needs_a_signed_in_user()
    {
        var response = await TestApi.Client(factory).GetAsync("/api/v1/me/activity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
