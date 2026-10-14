using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Frontend;

[Collection("Postgres collection")]
public class ErrorContractTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task A_validation_failure_is_keyed_by_the_camelCase_field_the_client_sent()
    {
        var response = await TestApi.Client(factory).PostAsJsonAsync("/api/v1/auth/register",
            new { email = "", password = "long-enough-password", fullName = "Someone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _), body.ToString());
        Assert.False(body.GetProperty("errors").TryGetProperty("Email", out _));
        Assert.True(body.TryGetProperty("correlationId", out _), body.ToString());
    }

    [Fact]
    public async Task An_amount_sent_as_a_string_is_a_400_that_names_no_internal_type()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = new StringContent(
                $$"""{ "memberUserId": "{{user.Id}}", "cycle": "2026-10", "amount": "100" }""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await TestApi.Client(factory, user.AccessToken).SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonDocument.Parse(text).RootElement;
        Assert.True(body.GetProperty("errors").TryGetProperty("amount", out _), text);
        Assert.True(body.TryGetProperty("correlationId", out _), text);
        Assert.DoesNotContain("Api.", text);
        Assert.DoesNotContain("Domain.", text);
        Assert.DoesNotContain("System.", text);
    }
}
