using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Api.Application.Contracts;
using Api.Application.Interfaces;
using Api.Application.Services;
using Domain;
using Tests.TestSupport;

namespace Tests.Integration;

/// <summary>
/// Refresh token rotation and reuse detection, against real PostgreSQL —
/// the atomic "mark used" step only means something with a real database.
/// </summary>
[Collection("Postgres collection")]
public class RefreshTokenTests
{
    private const string Password = "Pass123!";
    private readonly PostgresApiFactory _fixture;

    public RefreshTokenTests(PostgresApiFactory fixture)
    {
        _fixture = fixture;
    }

    // Each call gets its own scope, like each HTTP request would.
    private async Task<T> InScopeAsync<T>(Func<IAuthService, Task<T>> action)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAuthService>());
    }

    private async Task<(string Email, LoginResponse Tokens)> RegisterAndLoginAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        await InScopeAsync(a => a.RegisterAsync(new RegisterRequest(email, Password, "Refresh Test"), default));
        var login = await InScopeAsync(a => a.LoginAsync(new LoginRequest(email, Password), default));
        Assert.True(login.IsSuccess);
        return (email, login.Tokens!);
    }

    [Fact]
    public async Task Login_StoresOnlyTheHashOfTheRefreshToken()
    {
        var (_, tokens) = await RegisterAndLoginAsync();

        await using var db = _fixture.CreateContext();
        var stored = await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(tokens.RefreshToken));

        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Null(stored.UsedAt);
        Assert.Null(stored.RevokedAt);
        Assert.True(stored.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task Refresh_RotatesTheToken_AndKeepsTheFamily()
    {
        var (_, first) = await RegisterAndLoginAsync();

        var refreshed = await InScopeAsync(a => a.RefreshAsync(first.RefreshToken, default));

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(first.RefreshToken, refreshed.Tokens!.RefreshToken);

        await using var db = _fixture.CreateContext();
        var oldRow = await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(first.RefreshToken));
        var newRow = await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(refreshed.Tokens.RefreshToken));

        Assert.NotNull(oldRow.UsedAt);
        Assert.Equal(newRow.Id, oldRow.ReplacedByTokenId);
        Assert.Equal(oldRow.FamilyId, newRow.FamilyId);
        Assert.Null(newRow.UsedAt);
    }

    [Fact]
    public async Task ReusingAnExchangedToken_RevokesTheWholeFamily()
    {
        var (_, first) = await RegisterAndLoginAsync();
        var second = (await InScopeAsync(a => a.RefreshAsync(first.RefreshToken, default))).Tokens!;

        // The thief (or a stale client) presents the already-exchanged token.
        var reuse = await InScopeAsync(a => a.RefreshAsync(first.RefreshToken, default));
        Assert.False(reuse.IsSuccess);
        Assert.Equal(AuthService.InvalidSession, reuse.Error);

        // The newest token was descended from the same login, so it is dead too.
        var afterwards = await InScopeAsync(a => a.RefreshAsync(second.RefreshToken, default));
        Assert.False(afterwards.IsSuccess);

        await using var db = _fixture.CreateContext();
        var familyId = (await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(first.RefreshToken))).FamilyId;
        Assert.All(await db.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(),
            t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task TwoRequestsWithTheSameToken_OnlyOneSucceeds_AndTheFamilyDies()
    {
        var (_, first) = await RegisterAndLoginAsync();

        var results = await Task.WhenAll(
            InScopeAsync(a => a.RefreshAsync(first.RefreshToken, default)),
            InScopeAsync(a => a.RefreshAsync(first.RefreshToken, default)));

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        // Whichever token the winner received no longer works: the loser's attempt counted as reuse.
        var winner = results.Single(r => r.IsSuccess).Tokens!;
        var afterwards = await InScopeAsync(a => a.RefreshAsync(winner.RefreshToken, default));
        Assert.False(afterwards.IsSuccess);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var (_, tokens) = await RegisterAndLoginAsync();

        await using (var db = _fixture.CreateContext())
        {
            await db.RefreshTokens
                .Where(t => t.TokenHash == TokenService.Hash(tokens.RefreshToken))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var result = await InScopeAsync(a => a.RefreshAsync(tokens.RefreshToken, default));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthService.InvalidSession, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task MissingOrUnknownToken_IsRejected(string? raw)
    {
        var result = await InScopeAsync(a => a.RefreshAsync(raw, default));

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthService.InvalidSession, result.Error);
    }

    [Fact]
    public async Task WrongPassword_AndUnknownEmail_GiveTheSameMessage()
    {
        var (email, _) = await RegisterAndLoginAsync();

        var wrongPassword = await InScopeAsync(a => a.LoginAsync(new LoginRequest(email, "wrong-password"), default));
        var unknownEmail = await InScopeAsync(a => a.LoginAsync(new LoginRequest("nobody@test.local", Password), default));

        Assert.False(wrongPassword.IsSuccess);
        Assert.Equal(AuthService.InvalidCredentials, wrongPassword.Error);
        Assert.Equal(wrongPassword.Error, unknownEmail.Error);
    }
}
