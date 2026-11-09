using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>The 9.2 performance data: one stokvel with 40 made-up members and a year of contributions.</summary>
[Collection("Postgres collection")]
public class PerformanceSeedTests(PostgresApiFactory factory)
{
    private async Task<Guid> StokvelIdAsync()
    {
        await using var db = factory.CreateContext();
        return await db.Stokvels.Where(s => s.Name == PerformanceDemo.StokvelName).Select(s => s.Id).SingleAsync();
    }

    [Fact]
    public async Task The_stokvel_has_40_members_and_a_year_of_monthly_cycles()
    {
        var id = await StokvelIdAsync();
        await using var db = factory.CreateContext();

        Assert.Equal(PerformanceDemo.MemberCount, await db.StokvelMembers.CountAsync(m => m.StokvelId == id));
        Assert.Equal(PerformanceDemo.Months, await db.ContributionCycles.CountAsync(c => c.StokvelId == id));
        Assert.Equal(PerformanceDemo.Months - 1, await db.ContributionCycles.CountAsync(c => c.StokvelId == id && c.ClosedAt != null));
        Assert.Equal(1, await db.ContributionCycles.CountAsync(c => c.StokvelId == id && c.ClosedAt == null));

        // Everyone paid in each of the 11 closed months, and 28 of the 40 in the current one.
        var expected = (PerformanceDemo.Months - 1) * PerformanceDemo.MemberCount + PerformanceDemo.PaidInCurrentCycle;
        Assert.Equal(expected, await db.Contributions.CountAsync(c => c.StokvelId == id));
    }

    [Fact]
    public async Task Everyone_in_it_is_made_up_and_uses_a_dot_test_address()
    {
        var id = await StokvelIdAsync();
        await using var db = factory.CreateContext();

        var emails = await db.StokvelMembers.Where(m => m.StokvelId == id).Select(m => m.User.Email).ToListAsync();
        Assert.All(emails, e => Assert.EndsWith("@burial.test", e));
        Assert.Equal(emails.Count, emails.Distinct().Count());
    }

    [Fact]
    public async Task The_demo_accounts_homes_are_unchanged_by_it()
    {
        // Lerato is in exactly her two demo stokvels; the 40-member stokvel is not added to anyone's home but its own members'.
        var login = await TestApi.Client(factory).PostAsJsonAsync("/api/v1/auth/login",
            new { email = SeedData.LeratoEmail, password = factory.DemoPassword });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var home = await TestApi.Client(factory, token).GetFromJsonAsync<JsonElement>("/api/v1/me/home");

        Assert.Equal(2, home.GetProperty("stokvels").GetArrayLength());
    }

    [Fact]
    public async Task Its_chairperson_can_sign_in_with_the_demo_password_and_sees_the_stokvel_with_40_members()
    {
        var login = await TestApi.Client(factory).PostAsJsonAsync("/api/v1/auth/login",
            new { email = PerformanceDemo.ChairEmail, password = factory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var id = await StokvelIdAsync();
        var detail = await TestApi.Client(factory, token).GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{id}");
        Assert.Equal(PerformanceDemo.MemberCount, detail.GetProperty("memberCount").GetInt32());
        Assert.Equal(PerformanceDemo.MemberCount, detail.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task Running_it_again_adds_nothing_and_never_asks_for_a_password_hash()
    {
        async Task<(int, int, int)> Snapshot()
        {
            await using var db = factory.CreateContext();
            return (await db.Users.CountAsync(), await db.Stokvels.CountAsync(), await db.Contributions.CountAsync());
        }
        var before = await Snapshot();

        await using (var db = factory.CreateContext())
            await PerformanceDemo.EnsureAsync(
                db, NullLogger.Instance, () => throw new InvalidOperationException("Nothing should be added, so nothing should be hashed."), TimeProvider.System);

        Assert.Equal(before, await Snapshot());
    }
}
