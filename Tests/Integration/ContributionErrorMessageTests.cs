using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>The error text people see from POST /api/v1/stokvels/{id}/contributions.</summary>
[Collection("Postgres collection")]
public class ContributionErrorMessageTests(PostgresApiFactory factory)
{
    private static HttpRequestMessage RecordRequest(Guid stokvelId, Guid memberUserId, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { memberUserId, cycle = "2026-11", amountCents = 50000 }),
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    [Fact]
    public async Task A_duplicate_contribution_409_names_the_cycle_and_contains_no_ids()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);
        var client = TestApi.Client(factory, user.AccessToken);
        (await client.SendAsync(RecordRequest(stokvelId, user.Id, Guid.NewGuid().ToString()))).EnsureSuccessStatusCode();

        var response = await client.SendAsync(RecordRequest(stokvelId, user.Id, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = body.GetProperty("detail").GetString();
        Assert.Equal("A contribution has already been recorded for this member in cycle 2026-11.", detail);
        Assert.DoesNotContain(user.Id.ToString(), detail);
        Assert.DoesNotContain(stokvelId.ToString(), detail);
        Assert.Equal("contribution-already-recorded", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_missing_idempotency_key_400_has_exactly_the_message_with_no_parameter_suffix()
    {
        var user = await TestApi.RegisterAndLoginAsync(factory);
        var stokvelId = await TestApi.CreateStokvelAsync(factory, user);

        var response = await TestApi.Client(factory, user.AccessToken)
            .SendAsync(RecordRequest(stokvelId, user.Id, idempotencyKey: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("An Idempotency-Key header is required.", body.GetProperty("detail").GetString());
    }
}
