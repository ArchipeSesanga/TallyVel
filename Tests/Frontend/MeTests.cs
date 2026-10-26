using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class MeTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Me_returns_who_the_signed_in_member_is_and_never_the_password_hash()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);

        var response = await TestApi.Client(factory, user.AccessToken).GetAsync("/api/v1/me");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal("Leak Test", body.GetProperty("fullName").GetString());
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_without_a_token_is_a_401()
    {
        var response = await TestApi.Client(factory).GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_member_sees_their_role_per_stokvel_on_me_home_and_not_on_me()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        await TestApi.CreateStokvelAsync(factory, user);
        var client = TestApi.Client(factory, user.AccessToken);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
        var home = await client.GetFromJsonAsync<JsonElement>("/api/v1/me/home");

        Assert.False(me.TryGetProperty("role", out _));
        Assert.Equal("Chairperson", home.GetProperty("stokvels")[0].GetProperty("role").GetString());
    }
}
