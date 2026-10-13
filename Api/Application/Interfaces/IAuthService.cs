using Api.Application.Contracts;

namespace Api.Application.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest req, CancellationToken ct);
    Task<LoginResult> LoginAsync(LoginRequest req, CancellationToken ct);
}
