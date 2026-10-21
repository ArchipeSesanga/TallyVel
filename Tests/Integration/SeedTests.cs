using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>
/// The Development seeder: the demo users can sign in, seeding twice changes
/// nothing, and the data has the shape the frontend's sample data relies on.
/// The test host runs in Development, so the seed is already in the test database.
/// </summary>
[Collection("Postgres collection")]
public class SeedTests(PostgresApiFactory factory)
{
    private static readonly string[] DemoEmails =
    [
        "lerato@tallyvel.test", "thabo@tallyvel.test", "lindiwe@tallyvel.test",
        "sipho@tallyvel.test", "nomsa@tallyvel.test", "nandi@tallyvel.test",
    ];

    private async Task<string> LoginAsync(string email)
    {
        var response = await TestApi.Client(factory)
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password = factory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    [Fact]
    public async Task A_seeded_user_logs_in_with_the_demo_password_and_gets_an_access_token()
    {
        var response = await TestApi.Client(factory).PostAsJsonAsync(
            "/api/v1/auth/login", new { email = SeedData.LeratoEmail, password = factory.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Every_seeded_user_can_log_in()
    {
        await using var db = factory.CreateContext();
        var seededEmails = await db.Users.Where(u => u.Email.EndsWith("@tallyvel.test")).Select(u => u.Email).ToListAsync();
        Assert.Equal(DemoEmails.Order(), seededEmails.Order());

        foreach (var email in seededEmails)
            await LoginAsync(email);
    }

    [Fact]
    public async Task A_wrong_password_still_fails_for_a_seeded_user()
    {
        var response = await TestApi.Client(factory).PostAsJsonAsync(
            "/api/v1/auth/login", new { email = SeedData.LeratoEmail, password = factory.DemoPassword + "-wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        async Task<(int, int, int, int, string)> Snapshot()
        {
            await using var db = factory.CreateContext();
            var hashes = await db.Users.OrderBy(u => u.Email).Select(u => u.PasswordHash).ToListAsync();
            return (await db.Users.CountAsync(), await db.Stokvels.CountAsync(),
                await db.ContributionCycles.CountAsync(), await db.Contributions.CountAsync(),
                string.Join("|", hashes));
        }

        var before = await Snapshot();

        await using (var db = factory.CreateContext())
            await DatabaseSeeder.SeedAsync(
                db, NullLogger.Instance,
                () => throw new InvalidOperationException("Nothing should be seeded, so nothing should be hashed."),
                TimeProvider.System);

        Assert.Equal(before, await Snapshot());
    }

    [Fact]
    public async Task Lerato_is_Treasurer_of_one_stokvel_and_a_Member_of_another()
    {
        var home = await HomeAsync(await LoginAsync(SeedData.LeratoEmail));

        Assert.Equal("Treasurer", home[SeedData.FamilyStokvelName].GetProperty("role").GetString());
        Assert.Equal("Member", home[SeedData.WorkStokvelName].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Lerato_owes_in_one_current_cycle_and_is_paid_up_in_the_other()
    {
        var home = await HomeAsync(await LoginAsync(SeedData.LeratoEmail));
        var family = home[SeedData.FamilyStokvelName];
        var work = home[SeedData.WorkStokvelName];

        // Family Stokvel (R500): her contribution is Pending, which does not count, so she owes it all.
        Assert.Equal(JsonValueKind.Object, family.GetProperty("currentCycle").ValueKind);
        Assert.Equal(0, family.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(50000, family.GetProperty("owedThisCycleCents").GetInt64());

        // Work Stokvel (R300): paid in full.
        Assert.Equal(JsonValueKind.Object, work.GetProperty("currentCycle").ValueKind);
        Assert.Equal(30000, work.GetProperty("paidThisCycleCents").GetInt64());
        Assert.Equal(0, work.GetProperty("owedThisCycleCents").GetInt64());
    }

    [Fact]
    public async Task The_seed_has_all_three_statuses_a_10_cent_row_and_the_midnight_row()
    {
        await using var db = factory.CreateContext();
        var seeded = await db.Contributions.AsNoTracking()
            .Where(c => db.Users.Any(u => u.Id == c.MemberUserId && u.Email.EndsWith("@tallyvel.test")))
            .ToListAsync();

        Assert.Equal(
            [ContributionStatus.Pending, ContributionStatus.Paid, ContributionStatus.Confirmed],
            Enum.GetValues<ContributionStatus>().Where(s => seeded.Any(c => c.Status == s)));

        Assert.Contains(seeded, c => c.Amount == 0.10m && Money.ToCents(c.Amount) == 10);

        // 22:30 UTC on 31 October is 00:30 on 1 November in Johannesburg.
        var midnight = Assert.Single(seeded, c => c.RecordedAt == SeedData.MidnightBoundaryAt);
        var saDate = StokvelCalendar.Today(midnight.RecordedAt);
        Assert.Equal(new DateOnly(2026, 11, 1), saDate);

        // It sits in the cycle that contains that South African date: November, not October.
        var cycle = await db.ContributionCycles.AsNoTracking()
            .SingleAsync(c => c.StokvelId == midnight.StokvelId && c.Label == midnight.Cycle);
        Assert.Equal("2026-11", cycle.Label);
        Assert.True(cycle.StartsOn <= saDate && saDate <= cycle.EndsOn);
    }

    private async Task<Dictionary<string, JsonElement>> HomeAsync(string accessToken)
    {
        var response = await TestApi.Client(factory, accessToken).GetAsync("/api/v1/me/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stokvels")
            .EnumerateArray().ToDictionary(c => c.GetProperty("name").GetString()!);
    }
}
