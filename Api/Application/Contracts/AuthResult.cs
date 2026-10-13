namespace Api.Application.Contracts;

/// <summary>
/// Outcome of a login or refresh. Every failure carries the same kind of
/// message on purpose, so the API never reveals why a sign-in was refused.
/// </summary>
public sealed record AuthResult(LoginResponse? Tokens, string? Error)
{
    public bool IsSuccess => Tokens is not null;

    public static AuthResult Success(
        string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires) =>
        new(new LoginResponse(accessToken, accessExpires, refreshToken, refreshExpires), null);

    public static AuthResult Unauthorized(string error) => new((LoginResponse?)null, error);
}
