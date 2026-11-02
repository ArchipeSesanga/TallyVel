using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>GET /api/v1/stokvels/{id}/contributions with from, to, memberUserId and page tokens.</summary>
[Collection("Postgres collection")]
public class ContributionRangeTests(PostgresApiFactory factory)
{
    // The instants the tests care about. 22:00Z is midnight in South Africa (UTC+2).
    private static readonly DateTimeOffset RangeStart = new(2026, 9, 30, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2026, 10, 31, 22, 0, 0, TimeSpan.Zero);

    private async Task<(Guid StokvelId, TestUser Chair)> NewStokvelAsync()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        return (await TestApi.CreateStokvelAsync(factory, chair), chair);
    }

    /// <summary>Puts a contribution straight into the database, so its recordedAt is exactly what the test says.</summary>
    private async Task InsertAsync(Guid stokvelId, Guid memberId, string cycle, DateTimeOffset recordedAt)
    {
        await using var db = factory.CreateContext();
        // A contribution points at its cycle (a foreign key), so the cycle must exist first.
        if (!db.ContributionCycles.Any(c => c.StokvelId == stokvelId && c.Label == cycle))
            db.ContributionCycles.Add(new ContributionCycle(stokvelId, cycle));
        db.Contributions.Add(new Contribution(stokvelId, memberId, cycle, 100m, ContributionStatus.Paid, recordedAt));
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private Task<HttpResponseMessage> ListAsync(TestUser caller, Guid stokvelId, string query) =>
        TestApi.Client(factory, caller.AccessToken).GetAsync($"/api/v1/stokvels/{stokvelId}/contributions?{query}");

    private static string Z(DateTimeOffset instant) => instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    [Fact]
    public async Task From_is_included_and_to_is_excluded_so_a_contribution_exactly_at_to_is_left_out()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        await InsertAsync(stokvelId, chair.Id, "before", RangeStart.AddSeconds(-1));      // just before from: out
        await InsertAsync(stokvelId, chair.Id, "at-from", RangeStart);                     // exactly at from: IN
        await InsertAsync(stokvelId, chair.Id, "just-before-to", RangeEnd.AddSeconds(-1)); // just before to: IN
        await InsertAsync(stokvelId, chair.Id, "at-to", RangeEnd);                         // exactly at to: out

        var response = await ListAsync(chair, stokvelId, $"from={Z(RangeStart)}&to={Z(RangeEnd)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cycles = (await BodyAsync(response)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("cycle").GetString()).OrderBy(c => c).ToList();
        Assert.Equal(["at-from", "just-before-to"], cycles);
    }

    [Fact]
    public async Task Filtering_by_a_member_of_this_stokvel_returns_only_that_members_contributions()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var other = await TestApi.RegisterAndLoginAsync(factory);
        (await TestApi.Client(factory, chair.AccessToken).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members",
            new { userId = other.Id, role = "Member" })).EnsureSuccessStatusCode();
        await InsertAsync(stokvelId, chair.Id, "chair-pays", RangeStart);
        await InsertAsync(stokvelId, other.Id, "other-pays", RangeStart);

        var response = await ListAsync(chair, stokvelId, $"memberUserId={other.Id}");

        var cycles = (await BodyAsync(response)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("cycle").GetString()).ToList();
        Assert.Equal(["other-pays"], cycles);
    }

    [Fact]
    public async Task A_member_who_is_not_in_this_stokvel_gets_400_even_if_they_exist_in_another_one()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var (_, otherChair) = await NewStokvelAsync(); // a real user, but in a different stokvel

        var response = await ListAsync(chair, stokvelId, $"memberUserId={otherChair.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await BodyAsync(response);
        Assert.Equal("That member does not belong to this stokvel.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain(otherChair.Id.ToString(), body.ToString()); // nothing about the other stokvel's member
    }

    [Theory]
    [InlineData("from=yesterday")]
    [InlineData("to=2026-10-31")]                       // a bare date: which time zone?
    [InlineData("from=2026-10-31T22:00:00%2B02:00")]    // an offset instead of Z
    [InlineData("from=2026-10-31T22:00:00")]            // no Z at all
    [InlineData("to=2026-02-30T00:00:00Z")]             // a date that does not exist
    public async Task A_malformed_instant_gets_400(string query)
    {
        var (stokvelId, chair) = await NewStokvelAsync();

        var response = await ListAsync(chair, stokvelId, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("ending in Z", (await BodyAsync(response)).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("from=2026-10-31T22:00:00Z&to=2026-09-30T22:00:00Z")] // from after to
    [InlineData("from=2026-10-31T22:00:00Z&to=2026-10-31T22:00:00Z")] // from equal to to: an empty range
    public async Task From_that_is_not_earlier_than_to_gets_400(string query)
    {
        var (stokvelId, chair) = await NewStokvelAsync();

        var response = await ListAsync(chair, stokvelId, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("earlier", (await BodyAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_page_token_sent_with_a_different_filter_gets_400_with_the_mismatch_code()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        for (var i = 0; i < 3; i++) await InsertAsync(stokvelId, chair.Id, $"c{i}", RangeStart.AddMinutes(i));
        var first = await BodyAsync(await ListAsync(chair, stokvelId, "pageSize=1"));
        var token = first.GetProperty("nextPageToken").GetString()!;
        Assert.NotEmpty(token);

        // Same token, but now with a date range it was not made for.
        var response = await ListAsync(chair, stokvelId,
            $"pageSize=1&pageToken={Uri.EscapeDataString(token)}&from={Z(RangeStart)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("page-token-filter-mismatch", (await BodyAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_page_token_still_works_with_the_same_filter_and_walks_the_whole_range_once()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        for (var i = 0; i < 5; i++) await InsertAsync(stokvelId, chair.Id, $"c{i}", RangeStart.AddMinutes(i));
        var filter = $"pageSize=2&from={Z(RangeStart)}&to={Z(RangeEnd)}";

        var seen = new List<string>();
        string? token = null;
        do
        {
            var query = token is null ? filter : $"{filter}&pageToken={Uri.EscapeDataString(token)}";
            var body = await BodyAsync(await ListAsync(chair, stokvelId, query));
            seen.AddRange(body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("cycle").GetString()!));
            token = body.GetProperty("nextPageToken").GetString();
        } while (!string.IsNullOrEmpty(token));

        Assert.Equal(["c0", "c1", "c2", "c3", "c4"], seen); // none repeated, none skipped
    }

    [Fact]
    public async Task A_malformed_page_token_gets_400_with_its_own_code_not_the_mismatch_one()
    {
        var (stokvelId, chair) = await NewStokvelAsync();

        var response = await ListAsync(chair, stokvelId, "pageToken=not-a-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-pagetoken", (await BodyAsync(response)).GetProperty("code").GetString());
    }
}
