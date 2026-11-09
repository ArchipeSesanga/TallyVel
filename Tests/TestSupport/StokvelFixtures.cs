using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests.TestSupport;

/// <summary>Small helpers that build the situations the 9.1 tests need, through the real API.</summary>
public static class StokvelFixtures
{
    /// <summary>A stokvel with a new Chairperson. Returns the stokvel id and its creator.</summary>
    public static async Task<(Guid StokvelId, TestUser Chair)> NewStokvelAsync(WebApplicationFactory<Program> f)
    {
        var chair = await TestApi.RegisterAndLoginAsync(f);
        return (await TestApi.CreateStokvelAsync(f, chair), chair);
    }

    /// <summary>Registers a new person and adds them to the stokvel with the given role.</summary>
    public static async Task<TestUser> AddMemberAsync(
        WebApplicationFactory<Program> f, Guid stokvelId, TestUser chair, MemberRole role = MemberRole.Member)
    {
        var user = await TestApi.RegisterAndLoginAsync(f);
        (await TestApi.Client(f, chair.AccessToken).PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members",
            new { userId = user.Id, role = (int)role })).EnsureSuccessStatusCode();
        return user;
    }

    public static async Task<string> CreateInviteAsync(WebApplicationFactory<Program> f, Guid stokvelId, TestUser by)
    {
        var response = await TestApi.Client(f, by.AccessToken).PostAsync($"/api/v1/stokvels/{stokvelId}/invites", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    public static HttpRequestMessage WithKey(HttpMethod method, string url, string? key = null, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return request;
    }
}
