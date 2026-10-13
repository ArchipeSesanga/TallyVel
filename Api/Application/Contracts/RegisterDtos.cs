namespace Api.Application.Contracts;

public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record RegisterResponse(Guid Id, string Email, string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);


/// <summary>What the client sees after login or refresh. The refresh token travels only in the cookie.</summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
