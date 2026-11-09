using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;
using static Tests.TestSupport.StokvelFixtures;

namespace Tests.Integration;

/// <summary>Invite codes: create, preview and join.</summary>
[Collection("Postgres collection")]
public class InviteTests(PostgresApiFactory factory)
{
    private static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> InsertInviteAsync(Guid stokvelId, Guid createdBy, TimeSpan lifetime, bool revoked = false)
    {
        await using var db = factory.CreateContext();
        var invite = new StokvelInvite(stokvelId, createdBy, DateTimeOffset.UtcNow.AddDays(-30), lifetime);
        if (revoked) invite.Revoke(DateTimeOffset.UtcNow);
        db.StokvelInvites.Add(invite);
        await db.SaveChangesAsync();
        return invite.Code;
    }

    private Task<HttpResponseMessage> Preview(TestUser who, string code) =>
        TestApi.Client(factory, who.AccessToken).GetAsync($"/api/v1/invites/{code}");

    private Task<HttpResponseMessage> Join(TestUser who, string code, string? key = null) =>
        TestApi.Client(factory, who.AccessToken).SendAsync(WithKey(HttpMethod.Post, $"/api/v1/invites/{code}/join", key));

    // ---- creating ----

    [Fact]
    public async Task A_treasurer_creates_an_invite_that_expires_in_seven_days()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var treasurer = await AddMemberAsync(factory, stokvelId, chair, MemberRole.Treasurer);

        var response = await TestApi.Client(factory, treasurer.AccessToken).PostAsync($"/api/v1/stokvels/{stokvelId}/invites", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Body(response);
        var code = body.GetProperty("code").GetString()!;
        Assert.True(code.Length >= 10, code);
        Assert.Matches("^[A-Za-z0-9_-]+$", code); // URL-safe
        var days = (body.GetProperty("expiresAt").GetDateTimeOffset() - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(days, 6.9, 7.1);
        Assert.EndsWith("Z", body.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task Two_invites_never_share_a_code()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var codes = new[] { await CreateInviteAsync(factory, stokvelId, chair), await CreateInviteAsync(factory, stokvelId, chair) };
        Assert.NotEqual(codes[0], codes[1]);
    }

    [Fact]
    public async Task An_ordinary_member_cannot_create_an_invite_and_gets_403()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var member = await AddMemberAsync(factory, stokvelId, chair);

        var response = await TestApi.Client(factory, member.AccessToken).PostAsync($"/api/v1/stokvels/{stokvelId}/invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_non_member_cannot_create_an_invite_and_gets_404()
    {
        var (stokvelId, _) = await NewStokvelAsync(factory);
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, stranger.AccessToken).PostAsync($"/api/v1/stokvels/{stokvelId}/invites", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- preview ----

    [Fact]
    public async Task The_preview_has_only_the_allowed_fields_and_nothing_about_members()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var member = await AddMemberAsync(factory, stokvelId, chair);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);

        var response = await Preview(joiner, code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;
        var fields = body.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(
            ["alreadyMember", "code", "contributionAmountCents", "cycleLengthDays", "expiresAt", "frequency", "memberCount", "rules", "stokvelName"],
            fields);
        Assert.Equal("Leak Test Club", body.GetProperty("stokvelName").GetString());
        Assert.Equal(50000, body.GetProperty("contributionAmountCents").GetInt64());
        Assert.Equal(2, body.GetProperty("memberCount").GetInt32());
        Assert.False(body.GetProperty("alreadyMember").GetBoolean());
        // Nothing that identifies a member: no ids, no names.
        Assert.DoesNotContain(chair.Id.ToString(), text);
        Assert.DoesNotContain(member.Id.ToString(), text);
        Assert.DoesNotContain("Leak Test\"", text); // the members' display name
    }

    [Fact]
    public async Task The_preview_says_whether_the_caller_is_already_a_member()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);

        var body = await Body(await Preview(chair, code));

        Assert.True(body.GetProperty("alreadyMember").GetBoolean());
    }

    [Fact]
    public async Task A_stokvel_created_with_rules_shows_them_in_the_preview()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        var created = await TestApi.Client(factory, chair.AccessToken).PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Rules Club", contributionAmountCents = 20000, cycle = (int)ContributionFrequency.Weekly, creatorId = chair.Id,
            rules = "Pay by the 5th. Missed payments are repaid with the next one.",
        });
        created.EnsureSuccessStatusCode();
        var stokvelId = (await Body(created)).GetProperty("id").GetGuid();
        var code = await CreateInviteAsync(factory, stokvelId, chair);

        var body = await Body(await Preview(await TestApi.RegisterAndLoginAsync(factory), code));

        Assert.Equal("Pay by the 5th. Missed payments are repaid with the next one.", body.GetProperty("rules").GetString());
        Assert.Equal("Weekly", body.GetProperty("frequency").GetString());
        Assert.Equal(7, body.GetProperty("cycleLengthDays").GetInt32());
    }

    [Fact]
    public async Task Rules_longer_than_2000_characters_are_refused_with_400()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, chair.AccessToken).PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Wordy Club", contributionAmountCents = 20000, cycle = (int)ContributionFrequency.Monthly, creatorId = chair.Id,
            rules = new string('x', 2001),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_code_gets_404_with_the_invite_not_found_code()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);

        var response = await Preview(user, "this-code-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await Body(response);
        Assert.Equal("invite-not-found", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task An_expired_code_gets_410_with_the_invite_expired_code()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await InsertInviteAsync(stokvelId, chair.Id, TimeSpan.FromDays(7)); // made 30 days ago, lasted 7

        var response = await Preview(await TestApi.RegisterAndLoginAsync(factory), code);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("invite-expired", (await Body(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_revoked_code_gets_410_even_before_it_would_have_expired()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await InsertInviteAsync(stokvelId, chair.Id, TimeSpan.FromDays(90), revoked: true);

        var response = await Preview(await TestApi.RegisterAndLoginAsync(factory), code);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Guessing_codes_too_fast_gets_429_with_Retry_After()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        HttpResponseMessage? last = null;

        for (var i = 0; i < 15; i++) // the limit is 10 a minute per user
        {
            last = await Preview(user, $"guess-number-{i}-xxxxxxxx");
            if (last.StatusCode == HttpStatusCode.TooManyRequests) break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_preview_needs_a_signed_in_user()
    {
        var response = await TestApi.Client(factory).GetAsync("/api/v1/invites/anything-at-all");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- joining ----

    [Fact]
    public async Task Joining_adds_the_caller_as_a_Member_and_returns_201_with_the_stokvel_id()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);

        var response = await Join(joiner, code);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(stokvelId, (await Body(response)).GetProperty("stokvelId").GetGuid());
        await using var db = factory.CreateContext();
        var membership = await db.StokvelMembers.AsNoTracking().SingleAsync(m => m.StokvelId == stokvelId && m.UserId == joiner.Id);
        Assert.Equal(MemberRole.Member, membership.Role);
    }

    [Fact]
    public async Task Joining_when_already_a_member_gets_409_with_the_stokvel_id_in_the_problem()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);

        var response = await Join(chair, code);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await Body(response);
        Assert.Equal("already-member", body.GetProperty("code").GetString());
        Assert.Equal(stokvelId, body.GetProperty("stokvelId").GetGuid());
    }

    [Fact]
    public async Task Joining_with_an_unknown_code_gets_404_and_with_an_expired_code_gets_410()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var expired = await InsertInviteAsync(stokvelId, chair.Id, TimeSpan.FromDays(7));
        var joiner = await TestApi.RegisterAndLoginAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await Join(joiner, "no-such-invite-code")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await Join(joiner, expired)).StatusCode);
    }

    [Fact]
    public async Task Joining_without_an_Idempotency_Key_gets_400()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, joiner.AccessToken).PostAsync($"/api/v1/invites/{code}/join", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("An Idempotency-Key header is required.", (await Body(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Joining_again_with_the_same_key_replays_the_original_201_and_makes_one_membership()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);
        var key = Guid.NewGuid().ToString();

        var first = await Join(joiner, code, key);
        var second = await Join(joiner, code, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode); // a replay, not a 409
        await using var db = factory.CreateContext();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(m => m.StokvelId == stokvelId && m.UserId == joiner.Id));
    }

    [Fact]
    public async Task A_double_click_with_the_same_key_at_the_same_time_makes_one_membership_and_two_201s()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);
        var key = Guid.NewGuid().ToString();

        var both = await Task.WhenAll(Join(joiner, code, key), Join(joiner, code, key));

        Assert.All(both, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        await using var db = factory.CreateContext();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(m => m.StokvelId == stokvelId && m.UserId == joiner.Id));
    }

    [Fact]
    public async Task Two_joins_at_once_with_different_keys_make_one_membership_and_the_database_decides_the_loser()
    {
        var (stokvelId, chair) = await NewStokvelAsync(factory);
        var code = await CreateInviteAsync(factory, stokvelId, chair);
        var joiner = await TestApi.RegisterAndLoginAsync(factory);

        var both = await Task.WhenAll(Join(joiner, code), Join(joiner, code));

        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, both.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = factory.CreateContext();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(m => m.StokvelId == stokvelId && m.UserId == joiner.Id));
    }
}
