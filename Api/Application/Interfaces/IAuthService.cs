using Api.Application.Contracts;

namespace Api.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest req, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest req, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string? rawToken, CancellationToken ct);
}
