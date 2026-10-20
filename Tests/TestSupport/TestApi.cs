using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests.TestSupport;

public sealed record TestUser(Guid Id, string Email, string Password, string AccessToken, string RefreshToken);

public static class TestApi
{
    public static HttpClient Client(WebApplicationFactory<Program> f, string? accessToken = null)
    {
        var client = f.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        if (accessToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<TestUser> RegisterAndLoginAsync(WebApplicationFactory<Program> f)
    {
        var email = $"{Guid.NewGuid():N}@leak-test.local";
        const string password = "Leak-Test-Password-9071";
        var client = Client(f);

        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, fullName = "Leak Test" });
        reg.EnsureSuccessStatusCode();
        var id = (await reg.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var access = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        return new TestUser(id, email, password, access, RefreshCookie(login));
    }

    public static string RefreshCookie(HttpResponseMessage r) =>
        r.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("tallyvel_rt="))
            .Split(';')[0]["tallyvel_rt=".Length..];

    public static async Task<Guid> CreateStokvelAsync(WebApplicationFactory<Program> f, TestUser u)
    {
        // The API reads enums as numbers (no JsonStringEnumConverter), so send the value, not "Monthly".
        var r = await Client(f, u.AccessToken).PostAsJsonAsync("/api/v1/stokvels",
            new { name = "Leak Test Club", contributionAmountCents = 50000, cycle = (int)Domain.ContributionFrequency.Monthly, creatorId = u.Id });
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}