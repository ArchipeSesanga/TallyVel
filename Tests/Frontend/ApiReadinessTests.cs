using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Application.Services;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Tests.TestSupport;

namespace Tests.Frontend;

/// <summary>
/// What a frontend needs from the API (Assignment 7.1): money in integer cents,
/// contribution and cycle statuses, the current cycle as an object, timestamps
/// ending in Z and South African calendar dates.
/// </summary>
[Collection("Postgres collection")]
public class ApiReadinessTests(PostgresApiFactory factory)
{
    private static readonly Regex Timestamp = new("\"(\\d{4}-\\d{2}-\\d{2}T[^\"]+)\"", RegexOptions.Compiled);

    private HttpClient As(TestUser user) => TestApi.Client(factory, user.AccessToken);

    private static async Task<HttpResponseMessage> RecordAsync(
        HttpClient client, Guid stokvelId, Guid memberId, string cycle, long cents, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = memberId, cycle, amountCents = cents }),
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<Guid> OpenCycleAsync(HttpClient client, Guid stokvelId, string label)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> HomeCardAsync(TestUser user, Guid stokvelId)
    {
        var home = await As(user).GetFromJsonAsync<JsonElement>("/api/v1/me/home");
        return home.GetProperty("stokvels").EnumerateArray().Single(c => c.GetProperty("stokvelId").GetGuid() == stokvelId);
    }

    private static void AssertEveryTimestampEndsInZ(string json)
    {
        var matches = Timestamp.Matches(json).Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(matches);   // the check must have something to check
        foreach (var value in matches)
            Assert.EndsWith("Z", value);
    }

    // ---- Money: integer cents ---------------------------------------------------------

    [Theory]
    [InlineData(1)]       // R0.01
    [InlineData(10)]      // R0.10
    [InlineData(29)]      // a classic binary-float trap (0.29 * 100)
    [InlineData(1005)]    // R10.05
    [InlineData(10050)]
    [InlineData(Money.MaxCents)]
    public async Task A_contribution_comes_back_in_the_exact_cents_it_was_sent_with_status_Paid(long cents)
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);

        var response = await RecordAsync(As(user), stokvelId, user.Id, "2026-10", cents);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, body.GetProperty("amountCents").ValueKind);
        Assert.Equal(cents, body.GetProperty("amountCents").GetInt64());
        Assert.Equal("Paid", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("amount", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(Money.MaxCents + 1)]
    public async Task A_contribution_of_zero_cents_or_less_or_too_large_is_a_400(long cents)
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);

        var response = await RecordAsync(As(user), stokvelId, user.Id, "2026-10", cents);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("amountCents", out _), body.ToString());
    }

    [Fact]
    public async Task A_stokvel_is_created_and_returned_with_its_contribution_amount_in_cents()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);

        var response = await As(user).PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Cents Club", contributionAmountCents = 123456, cycle = (int)ContributionFrequency.Weekly, creatorId = user.Id,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(123456, body.GetProperty("contributionAmountCents").GetInt64());
        Assert.False(body.TryGetProperty("contributionAmount", out _));
    }

    [Fact]
    public async Task A_stokvel_of_zero_cents_is_a_400()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);

        var response = await As(user).PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Free Club", contributionAmountCents = 0, cycle = (int)ContributionFrequency.Monthly, creatorId = user.Id,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_contribution_list_filters_by_amount_in_cents_and_returns_cents()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = As(user);
        foreach (var (cycle, cents) in new[] { ("c1", 1000L), ("c2", 5000L), ("c3", 9000L) })
            (await RecordAsync(client, stokvelId, user.Id, cycle, cents)).EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/stokvels/{stokvelId}/contributions?minAmountCents=2000&maxAmountCents=6000");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        var only = Assert.Single(items);
        Assert.Equal(5000, only.GetProperty("amountCents").GetInt64());
        Assert.Equal("Paid", only.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Retrying_a_contribution_with_the_same_key_replays_the_same_cents_and_status()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var key = Guid.NewGuid().ToString();

        var first = await (await RecordAsync(As(user), stokvelId, user.Id, "2026-10", 10050, key))
            .Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await RecordAsync(As(user), stokvelId, user.Id, "2026-10", 10050, key))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(10050, second.GetProperty("amountCents").GetInt64());
        Assert.Equal("Paid", second.GetProperty("status").GetString());
        Assert.Equal(first.GetProperty("recordedAt").GetString(), second.GetProperty("recordedAt").GetString());
    }

    [Fact]
    public async Task A_payout_comes_back_in_cents()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = As(user);
        var cycleId = await OpenCycleAsync(client, stokvelId, "2026-10");
        (await RecordAsync(client, stokvelId, user.Id, "2026-10", 12345)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", new { recipientUserId = user.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12345, body.GetProperty("amountCents").GetInt64());
        Assert.False(body.TryGetProperty("amount", out _));
    }

    // ---- Which statuses count -----------------------------------------------------------

    [Theory]
    [InlineData(ContributionStatus.Pending, 0)]
    [InlineData(ContributionStatus.Paid, 10000)]
    [InlineData(ContributionStatus.Confirmed, 10000)]
    public async Task Only_Paid_and_Confirmed_contributions_count_towards_paid_and_owed_on_the_home_screen(
        ContributionStatus status, long expectedPaidCents)
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);   // R500 a cycle
        await OpenCycleAsync(As(user), stokvelId, "2026-10");
        await using (var db = factory.CreateContext())
        {
            db.Contributions.Add(new Contribution(stokvelId, user.Id, "2026-10", 100m, status));
            await db.SaveChangesAsync();
        }

        var card = await HomeCardAsync(user, stokvelId);

        Assert.Equal(expectedPaidCents, card.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(50000 - expectedPaidCents, card.GetProperty("owedThisCycleCents").GetInt64());
    }

    [Fact]
    public async Task A_Pending_contribution_is_not_paid_out()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = As(user);
        var cycleId = await OpenCycleAsync(client, stokvelId, "2026-10");
        await using (var db = factory.CreateContext())
        {
            db.Contributions.Add(new Contribution(stokvelId, user.Id, "2026-10", 100m, ContributionStatus.Pending));
            await db.SaveChangesAsync();
        }

        var payout = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", new { recipientUserId = user.Id });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, payout.StatusCode);   // nothing counted, nothing to pay out
    }

    // ---- The current cycle is an object -------------------------------------------------

    [Fact]
    public async Task The_current_cycle_is_an_object_with_South_African_dates()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);   // Monthly
        var cycleId = await OpenCycleAsync(As(user), stokvelId, "2026-10");
        var today = StokvelCalendar.Today(TimeProvider.System.GetUtcNow());

        var cycle = (await HomeCardAsync(user, stokvelId)).GetProperty("currentCycle");

        Assert.Equal(cycleId, cycle.GetProperty("id").GetGuid());
        Assert.Equal("2026-10", cycle.GetProperty("label").GetString());
        Assert.Equal("Open", cycle.GetProperty("status").GetString());
        Assert.Equal(today.ToString("yyyy-MM-dd"), cycle.GetProperty("startsOn").GetString());
        // endsOn is the due date: the last day of the (monthly) cycle.
        Assert.Equal(
            StokvelCalendar.EndOfCycle(today, ContributionFrequency.Monthly).ToString("yyyy-MM-dd"),
            cycle.GetProperty("endsOn").GetString());
    }

    [Fact]
    public async Task With_no_open_cycle_the_current_cycle_is_null_and_nothing_is_paid_or_owed()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);   // R500, no cycle opened

        var card = await HomeCardAsync(user, stokvelId);

        Assert.Equal(JsonValueKind.Null, card.GetProperty("currentCycle").ValueKind);
        Assert.Equal(0, card.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(0, card.GetProperty("owedThisCycleCents").GetInt64());
        Assert.Equal(JsonValueKind.Null, card.GetProperty("nextPayoutDate").ValueKind);
    }

    [Fact]
    public async Task A_closed_cycle_is_not_the_current_cycle()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var cycleId = await OpenCycleAsync(As(user), stokvelId, "2026-10");
        await CloseAsync(cycleId);

        var card = await HomeCardAsync(user, stokvelId);

        Assert.Equal(JsonValueKind.Null, card.GetProperty("currentCycle").ValueKind);
        Assert.Equal(0, card.GetProperty("owedThisCycleCents").GetInt64());
    }

    [Fact]
    public async Task A_cycle_that_has_not_started_yet_is_not_the_current_cycle()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var today = StokvelCalendar.Today(TimeProvider.System.GetUtcNow());
        await using (var db = factory.CreateContext())
        {
            db.ContributionCycles.Add(new ContributionCycle(stokvelId, "next", today.AddDays(30), today.AddDays(59)));
            await db.SaveChangesAsync();
        }

        var card = await HomeCardAsync(user, stokvelId);

        Assert.Equal(JsonValueKind.Null, card.GetProperty("currentCycle").ValueKind);
    }

    // ---- Cycle status: Open, Closed, PaidOut --------------------------------------------

    private async Task CloseAsync(Guid cycleId)
    {
        await using var db = factory.CreateContext();
        await db.ContributionCycles.Where(c => c.Id == cycleId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAt, DateTimeOffset.UtcNow));
    }

    private static async Task<string> CycleStatusAsync(HttpClient client, Guid stokvelId, Guid cycleId)
    {
        var cycles = await client.GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{stokvelId}/cycles");
        return cycles.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == cycleId)
            .GetProperty("status").GetString()!;
    }

    [Fact]
    public async Task A_cycle_is_Open_then_Closed_then_PaidOut()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = As(user);
        var cycleId = await OpenCycleAsync(client, stokvelId, "2026-10");
        (await RecordAsync(client, stokvelId, user.Id, "2026-10", 5000)).EnsureSuccessStatusCode();

        Assert.Equal("Open", await CycleStatusAsync(client, stokvelId, cycleId));

        await CloseAsync(cycleId);   // what the cycle-close job does
        Assert.Equal("Closed", await CycleStatusAsync(client, stokvelId, cycleId));

        (await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout",
            new { recipientUserId = user.Id })).EnsureSuccessStatusCode();
        Assert.Equal("PaidOut", await CycleStatusAsync(client, stokvelId, cycleId));   // paid out wins over closed
    }

    [Fact]
    public async Task The_database_filter_for_an_open_cycle_agrees_with_the_status_rule_in_every_state()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        (bool PaidOut, bool Closed)[] states = [(false, false), (false, true), (true, false), (true, true)];

        await using var db = factory.CreateContext();
        var cycles = states.Select((s, i) => (State: s, Cycle: new ContributionCycle(stokvelId, $"state{i}"))).ToList();
        db.ContributionCycles.AddRange(cycles.Select(c => c.Cycle));
        await db.SaveChangesAsync();
        foreach (var (state, cycle) in cycles)
            await db.ContributionCycles.Where(c => c.Id == cycle.Id).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsPaidOut, state.PaidOut)
                .SetProperty(c => c.ClosedAt, state.Closed ? DateTimeOffset.UtcNow : (DateTimeOffset?)null));

        var loaded = await db.ContributionCycles.AsNoTracking().Where(c => c.StokvelId == stokvelId).ToListAsync();
        var openInDatabase = (await db.ContributionCycles.Where(c => c.StokvelId == stokvelId)
            .Where(ContributionCycle.IsOpen).Select(c => c.Id).ToListAsync()).ToHashSet();

        Assert.Equal(4, loaded.Count);
        foreach (var cycle in loaded)
            Assert.Equal(cycle.Status == CycleStatus.Open, openInDatabase.Contains(cycle.Id));
        Assert.Equal(
            [CycleStatus.Open, CycleStatus.Closed, CycleStatus.PaidOut, CycleStatus.PaidOut],
            cycles.Select(c => loaded.Single(l => l.Id == c.Cycle.Id).Status));
    }

    // ---- Timestamps end in Z, dates are South African ------------------------------------

    [Fact]
    public async Task Every_timestamp_in_every_kind_of_response_ends_in_Z()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = As(user);
        var cycleId = await OpenCycleAsync(client, stokvelId, "2026-10");

        var login = await TestApi.Client(factory)
            .PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });
        var contribution = await RecordAsync(client, stokvelId, user.Id, "2026-10", 5000);
        var list = await client.GetAsync($"/api/v1/stokvels/{stokvelId}/contributions");
        var stokvel = await client.GetAsync($"/api/v1/stokvels/{stokvelId}");
        var payout = await client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout", new { recipientUserId = user.Id });

        foreach (var response in new[] { login, contribution, list, stokvel, payout })
        {
            Assert.True(response.IsSuccessStatusCode, response.ToString());
            AssertEveryTimestampEndsInZ(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task A_timestamp_is_written_with_a_Z_and_not_a_zero_offset()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);

        var text = await (await RecordAsync(As(user), stokvelId, user.Id, "2026-10", 5000))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain("+00:00", text);
        Assert.Matches("\"recordedAt\":\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(\\.\\d+)?Z\"", text);
    }

    [Fact]
    public async Task The_seeded_midnight_contribution_is_listed_as_exactly_2026_10_31_22_30_Z()
    {
        var token = (await (await TestApi.Client(factory).PostAsJsonAsync("/api/v1/auth/login",
                new { email = Infrastructure.Persistence.SeedData.LeratoEmail, password = factory.DemoPassword }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = TestApi.Client(factory, token);
        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/home");
        var familyId = home.GetProperty("stokvels").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == Infrastructure.Persistence.SeedData.FamilyStokvelName)
            .GetProperty("stokvelId").GetGuid();

        var text = await client.GetStringAsync($"/api/v1/stokvels/{familyId}/contributions?cycle=2026-11");

        Assert.Contains("\"recordedAt\":\"2026-10-31T22:30:00Z\"", text);
    }

    [Fact]
    public async Task The_next_payout_date_is_a_South_African_calendar_date()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var weekly = await As(user).PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Weekly Club", contributionAmountCents = 10000, cycle = (int)ContributionFrequency.Weekly, creatorId = user.Id,
        });
        weekly.EnsureSuccessStatusCode();
        var created = await weekly.Content.ReadFromJsonAsync<JsonElement>();
        var stokvelId = created.GetProperty("id").GetGuid();
        await OpenCycleAsync(As(user), stokvelId, "2026-10");

        // Payouts fall every 7 days from the South African creation date, D0. Move the clock to
        // 22:30 UTC on D0+7, which is already D0+8 in Johannesburg. Today is D0+8, so the next
        // payout is D0+14. (A UTC "today" of D0+7 would wrongly say D0+7.)
        var d0 = StokvelCalendar.Today(created.GetProperty("createdAt").GetDateTimeOffset());
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(new DateTimeOffset(d0.AddDays(7).ToDateTime(new TimeOnly(22, 30)), TimeSpan.Zero));

        await using var db = factory.CreateContext();
        var home = await new MemberHomeService(db, clock).GetAsync(user.Id, CancellationToken.None);

        var card = home.Stokvels.Single(c => c.StokvelId == stokvelId);
        Assert.Equal(d0.AddDays(14), card.NextPayoutDate);
    }
}
