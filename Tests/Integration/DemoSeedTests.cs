using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>The 9.1 demo data: Thandi and her two invites, Sipho's two roles, the two proofs and the stokvel rules.</summary>
[Collection("Postgres collection")]
public class DemoSeedTests(PostgresApiFactory factory)
{
    private async Task<HttpClient> SignedInAsync(string email)
    {
        var login = await TestApi.Client(factory).PostAsJsonAsync("/api/v1/auth/login", new { email, password = factory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        return TestApi.Client(factory, token);
    }

    private async Task<Guid> StokvelIdAsync(string name)
    {
        await using var db = factory.CreateContext();
        return await db.Stokvels.Where(s => s.Name == name).Select(s => s.Id).SingleAsync();
    }

    [Fact]
    public async Task Thandi_exists_can_sign_in_and_is_in_no_stokvel()
    {
        var thandi = await SignedInAsync(DemoAdditions.ThandiEmail);

        var home = await thandi.GetFromJsonAsync<JsonElement>("/api/v1/me/home");

        Assert.Empty(home.GetProperty("stokvels").EnumerateArray());
    }

    [Fact]
    public async Task The_seed_has_one_valid_invite_to_Family_and_one_expired_invite_to_Work_that_Thandi_can_see()
    {
        var thandi = await SignedInAsync(DemoAdditions.ThandiEmail);
        var familyId = await StokvelIdAsync(SeedData.FamilyStokvelName);
        var workId = await StokvelIdAsync(SeedData.WorkStokvelName);
        await using var db = factory.CreateContext();
        var now = DateTimeOffset.UtcNow;
        var valid = await db.StokvelInvites.AsNoTracking().SingleAsync(i => i.StokvelId == familyId && i.ExpiresAt > now);
        var expired = await db.StokvelInvites.AsNoTracking().SingleAsync(i => i.StokvelId == workId && i.ExpiresAt < now);

        var ok = await thandi.GetAsync($"/api/v1/invites/{valid.Code}");
        var gone = await thandi.GetAsync($"/api/v1/invites/{expired.Code}");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var preview = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(SeedData.FamilyStokvelName, preview.GetProperty("stokvelName").GetString());
        Assert.False(preview.GetProperty("alreadyMember").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(preview.GetProperty("rules").GetString()));
        Assert.Equal(HttpStatusCode.Gone, gone.StatusCode);
    }

    [Fact]
    public async Task Sipho_is_Treasurer_of_Work_Stokvel_and_an_ordinary_Member_of_Family_Stokvel()
    {
        var sipho = await SignedInAsync(DemoAdditions.SiphoEmail);

        var home = await sipho.GetFromJsonAsync<JsonElement>("/api/v1/me/home");
        var roles = home.GetProperty("stokvels").EnumerateArray()
            .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.GetProperty("role").GetString());

        Assert.Equal("Treasurer", roles[SeedData.WorkStokvelName]);
        Assert.Equal("Member", roles[SeedData.FamilyStokvelName]);
    }

    [Fact]
    public async Task Each_of_those_stokvels_has_another_members_contribution_with_a_proof_and_only_Work_lets_Sipho_download_it()
    {
        var sipho = await SignedInAsync(DemoAdditions.SiphoEmail);
        var siphoId = (await sipho.GetFromJsonAsync<JsonElement>("/api/v1/me")).GetProperty("id").GetGuid();

        foreach (var (name, canDownload) in new[] { (SeedData.WorkStokvelName, true), (SeedData.FamilyStokvelName, false) })
        {
            var stokvelId = await StokvelIdAsync(name);
            var list = await sipho.GetFromJsonAsync<JsonElement>($"/api/v1/stokvels/{stokvelId}/contributions?pageSize=100");
            var withProof = list.GetProperty("items").EnumerateArray().Where(c => c.GetProperty("hasProof").GetBoolean()).ToList();

            var item = Assert.Single(withProof);
            Assert.NotEqual(siphoId, item.GetProperty("memberUserId").GetGuid()); // another member's, not Sipho's own
            Assert.Equal(canDownload, item.GetProperty("canDownloadProof").GetBoolean());

            var download = await sipho.GetAsync($"/api/v1/stokvels/{stokvelId}/contributions/{item.GetProperty("id").GetGuid()}/proof");
            Assert.Equal(canDownload ? HttpStatusCode.OK : HttpStatusCode.NotFound, download.StatusCode);
        }
    }

    [Fact]
    public async Task The_demo_stokvels_have_rules()
    {
        await using var db = factory.CreateContext();

        var stokvels = await db.Stokvels.AsNoTracking()
            .Where(s => s.Name == SeedData.FamilyStokvelName || s.Name == SeedData.WorkStokvelName).ToListAsync();

        Assert.Equal(2, stokvels.Count);
        Assert.All(stokvels, s => Assert.False(string.IsNullOrWhiteSpace(s.Rules)));
    }

    [Fact]
    public async Task Running_the_demo_additions_again_adds_nothing_and_never_asks_for_a_password_hash()
    {
        async Task<(int, int, int, int)> Counts()
        {
            await using var db = factory.CreateContext();
            return (await db.Users.CountAsync(), await db.StokvelInvites.CountAsync(),
                await db.ContributionProofs.CountAsync(), await db.StokvelMembers.CountAsync());
        }
        var before = await Counts();

        await using (var db = factory.CreateContext())
            await DemoAdditions.EnsureAsync(db, NullLogger.Instance,
                () => throw new InvalidOperationException("Nothing should be created, so nothing should be hashed."), TimeProvider.System);

        Assert.Equal(before, await Counts());
    }

    [Fact]
    public async Task Lerato_is_still_Treasurer_in_Family_and_a_Member_in_Work_after_the_additions()
    {
        var lerato = await SignedInAsync(SeedData.LeratoEmail);

        var home = await lerato.GetFromJsonAsync<JsonElement>("/api/v1/me/home");
        var roles = home.GetProperty("stokvels").EnumerateArray()
            .ToDictionary(s => s.GetProperty("name").GetString()!, s => s.GetProperty("role").GetString());

        Assert.Equal("Treasurer", roles[SeedData.FamilyStokvelName]);
        Assert.Equal("Member", roles[SeedData.WorkStokvelName]);
    }
}
