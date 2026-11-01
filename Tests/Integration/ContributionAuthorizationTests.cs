using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Microsoft.EntityFrameworkCore;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>Who may record a contribution for whom: POST /api/v1/stokvels/{id}/contributions.</summary>
[Collection("Postgres collection")]
public class ContributionAuthorizationTests(PostgresApiFactory factory)
{
    // The chairperson created the stokvel; the other users are added to it with the given role.
    private async Task<(Guid StokvelId, TestUser Chair)> NewStokvelAsync()
    {
        var chair = await TestApi.RegisterAndLoginAsync(factory);
        return (await TestApi.CreateStokvelAsync(factory, chair), chair);
    }

    private async Task<TestUser> AddMemberAsync(Guid stokvelId, TestUser chair, MemberRole role)
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var added = await TestApi.Client(factory, chair.AccessToken)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId = user.Id, role = (int)role });
        added.EnsureSuccessStatusCode();
        return user;
    }

    private Task<HttpResponseMessage> RecordAsync(Guid stokvelId, TestUser caller, Guid forUserId, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId = forUserId, cycle = "2026-10", amountCents = 50000 }),
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return TestApi.Client(factory, caller.AccessToken).SendAsync(request);
    }

    private async Task<int> ContributionRowsAsync(Guid stokvelId)
    {
        await using var db = factory.CreateContext();
        return await db.Contributions.CountAsync(c => c.StokvelId == stokvelId);
    }

    [Fact]
    public async Task A_member_recording_for_themselves_gets_201()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var member = await AddMemberAsync(stokvelId, chair, MemberRole.Member);

        var response = await RecordAsync(stokvelId, member, forUserId: member.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_member_recording_for_another_member_gets_403_and_no_contribution_is_created()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var member = await AddMemberAsync(stokvelId, chair, MemberRole.Member);
        var other = await AddMemberAsync(stokvelId, chair, MemberRole.Member);

        var response = await RecordAsync(stokvelId, member, forUserId: other.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Treasurer", body.GetProperty("detail").GetString());
        Assert.True(body.TryGetProperty("correlationId", out _), body.ToString());
        Assert.Equal(0, await ContributionRowsAsync(stokvelId));
    }

    [Fact]
    public async Task A_treasurer_recording_for_another_member_gets_201()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var treasurer = await AddMemberAsync(stokvelId, chair, MemberRole.Treasurer);
        var member = await AddMemberAsync(stokvelId, chair, MemberRole.Member);

        var response = await RecordAsync(stokvelId, treasurer, forUserId: member.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_non_member_of_the_stokvel_gets_404()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        var response = await RecordAsync(stokvelId, stranger, forUserId: stranger.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Recording_for_a_user_who_is_not_a_member_of_this_stokvel_gets_422()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var treasurer = await AddMemberAsync(stokvelId, chair, MemberRole.Treasurer);
        var outsider = await TestApi.RegisterAndLoginAsync(factory);

        var response = await RecordAsync(stokvelId, treasurer, forUserId: outsider.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("does not belong", body.GetProperty("detail").GetString());
        Assert.Equal(0, await ContributionRowsAsync(stokvelId));
    }

    [Fact]
    public async Task A_refused_403_does_not_use_up_the_idempotency_key_so_a_treasurer_retry_with_it_succeeds()
    {
        var (stokvelId, chair) = await NewStokvelAsync();
        var treasurer = await AddMemberAsync(stokvelId, chair, MemberRole.Treasurer);
        var member = await AddMemberAsync(stokvelId, chair, MemberRole.Member);
        var other = await AddMemberAsync(stokvelId, chair, MemberRole.Member);
        var key = Guid.NewGuid().ToString();

        var refused = await RecordAsync(stokvelId, member, forUserId: other.Id, key);
        var retry = await RecordAsync(stokvelId, treasurer, forUserId: other.Id, key);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, await ContributionRowsAsync(stokvelId));
    }
}
