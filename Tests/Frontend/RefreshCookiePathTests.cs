using System.Net;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class RefreshCookiePathTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Refresh_works_at_the_versioned_path_and_the_cookie_is_scoped_to_it()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"tallyvel_rt={user.RefreshToken}");
        var response = await TestApi.Client(factory).SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tallyvel_rt="));
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
