using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Integration;

[Collection("Postgres collection")]
public class ObservabilityTests(PostgresApiFactory factory)
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string TraceParent = $"00-{TraceId}-00f067aa0ba902b7-01";

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Probes_answer_without_a_token(string path)
    {
        var response = await TestApi.Client(factory).GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_401_carries_the_callers_trace_id()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stokvels/{Guid.NewGuid()}/cycles");
        request.Headers.Add("traceparent", TraceParent);

        var response = await TestApi.Client(factory).SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TraceId, problem.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Non_member_404_carries_the_callers_trace_id()
    {
        var owner = await TestApi.RegisterAndLoginAsync(factory);
        var stranger = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, owner);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stokvels/{stokvelId}/cycles");
        request.Headers.Add("traceparent", TraceParent);
        var response = await TestApi.Client(factory, stranger.AccessToken).SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TraceId, problem.GetProperty("correlationId").GetString());
    }
}