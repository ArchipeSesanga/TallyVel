using System.Net;
using System.Net.Http.Json;
using Domain;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>Leaving a stokvel: DELETE /api/v1/stokvels/{id}/members/{userId} where the user is the caller.</summary>
[Collection("Postgres collection")]
public class LeaveStokvelTests(PostgresApiFactory factory)
{
    private HttpClient As(TestUser u) => TestApi.Client(factory, u.AccessToken);
    private static string Member(Guid stokvelId, Guid userId) => $"/api/v1/stokvels/{stokvelId}/members/{userId}";

    [Fact]
    public async Task An_ordinary_member_can_leave_and_is_then_a_stranger_to_the_stokvel()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var member = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair);

        var left = await As(member).DeleteAsync(Member(stokvelId, member.Id));
        Assert.Equal(HttpStatusCode.OK, left.StatusCode);

        // No longer a member: the stokvel looks like it does not exist to them (404), as for any non-member.
        Assert.Equal(HttpStatusCode.NotFound, (await As(member).GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
        // The chairperson is still there.
        Assert.Equal(HttpStatusCode.OK, (await As(chair).GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
    }

    [Fact]
    public async Task A_member_cannot_remove_somebody_else()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var one = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair);
        var two = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair);

        Assert.Equal(HttpStatusCode.Forbidden, (await As(one).DeleteAsync(Member(stokvelId, two.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(two).GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
    }

    [Fact]
    public async Task The_chairperson_can_still_remove_a_member()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var member = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair);

        Assert.Equal(HttpStatusCode.OK, (await As(chair).DeleteAsync(Member(stokvelId, member.Id))).StatusCode);
    }

    [Fact]
    public async Task The_last_chairperson_cannot_leave_so_the_stokvel_always_has_someone_to_run_it()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);

        var response = await As(chair).DeleteAsync(Member(stokvelId, chair.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(chair).GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
    }

    [Fact]
    public async Task A_chairperson_can_leave_once_somebody_else_is_chairperson()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var other = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair, MemberRole.Chairperson);

        Assert.Equal(HttpStatusCode.OK, (await As(chair).DeleteAsync(Member(stokvelId, chair.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await As(other).GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
    }

    [Fact]
    public async Task Someone_who_is_not_a_member_gets_404_when_trying_to_leave_and_when_removing_others()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var stranger = await TestApi.RegisterAndLoginAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await As(stranger).DeleteAsync(Member(stokvelId, stranger.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await As(stranger).DeleteAsync(Member(stokvelId, chair.Id))).StatusCode);
    }

    [Fact]
    public async Task Leaving_keeps_the_stokvel_and_the_past_contributions()
    {
        var (stokvelId, chair) = await StokvelFixtures.NewStokvelAsync(factory);
        var member = await StokvelFixtures.AddMemberAsync(factory, stokvelId, chair);
        (await As(chair).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { label = "2026-11" })).EnsureSuccessStatusCode();
        var recorded = StokvelFixtures.WithKey(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions", null,
            JsonContent.Create(new { memberUserId = member.Id, cycle = "2026-11", amountCents = 50000 }));
        Assert.Equal(HttpStatusCode.Created, (await As(chair).SendAsync(recorded)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await As(member).DeleteAsync(Member(stokvelId, member.Id))).StatusCode);

        var list = await As(chair).GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/stokvels/{stokvelId}/contributions");
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
    }
}
