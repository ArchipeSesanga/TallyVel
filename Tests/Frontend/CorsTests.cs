using System.Net;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class CorsTests(PostgresApiFactory factory)
{
    private const string Frontend = "http://localhost:3000";

    private static HttpRequestMessage Preflight(string origin, string path) =>
        new(HttpMethod.Options, path)
        {
            Headers =
            {
                { "Origin", origin },
                { "Access-Control-Request-Method", "POST" },
                { "Access-Control-Request-Headers", "idempotency-key,content-type,authorization" },
            },
        };

    [Fact]
    public async Task A_preflight_from_the_frontend_is_allowed_with_credentials()
    {
        var response = await TestApi.Client(factory)
            .SendAsync(Preflight(Frontend, $"/api/v1/stokvels/{Guid.NewGuid()}/contributions"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Frontend, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("idempotency-key", allowedHeaders, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_preflight_from_another_origin_gets_no_allow_origin()
    {
        var response = await TestApi.Client(factory)
            .SendAsync(Preflight("http://evil.example", $"/api/v1/stokvels/{Guid.NewGuid()}/contributions"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task An_error_response_still_carries_cors_headers()
    {
        var owner = await TestApi.RegisterAndLoginAsync(factory);
        var stranger = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, owner);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stokvels/{stokvelId}/cycles");
        request.Headers.Add("Origin", Frontend);
        var response = await TestApi.Client(factory, stranger.AccessToken).SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Frontend, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
