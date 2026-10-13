using Api.Application.Contracts;

namespace Api.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest req, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest req, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string? rawToken, CancellationToken ct);

    /// <summary>Revokes the whole family of the given refresh token. Does nothing if the token is missing or unknown.</summary>
    Task LogoutAsync(string? rawToken, CancellationToken ct);
}
