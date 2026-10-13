using Api.Application.Contracts;
using Api.Application.Interfaces;
using Domain;
using Microsoft.AspNetCore.Identity;

namespace Api.Application.Services;

public class AuthService(IUserRepository users, IPasswordHasher<User> hasher, ITokenService tokens) : IAuthService
{
    public async Task<RegisterResult> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = EmailNormalizer.Normalize(req.Email);

        if (await users.EmailExistsAsync(email, ct))
            return RegisterResult.DuplicateEmail();

        // User only accepts an already-hashed password, so there is no User
        // yet to hash for. The default PasswordHasher ignores the user
        // argument, so null is safe here.
        var passwordHash = hasher.HashPassword(null!, req.Password);
        var user = new User(email, req.FullName, passwordHash);

        try
        {
            await users.AddAsync(user, ct);
        }
        catch (AlreadyExistsException)
        {
            // Two requests with the same email at the same moment: the pre-check passed for both, the index caught one
            return RegisterResult.DuplicateEmail();
        }

        return RegisterResult.Success(new RegisterResponse(user.Id, user.Email, user.FullName));
    }

    // Verified against this when the email is unknown, so a missing account
    // takes about as long to reject as a wrong password.
    // Computed once per process, not per request.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString()));

    public async Task<LoginResult> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = EmailNormalizer.Normalize(req.Email);
        var user = await users.GetByEmailAsync(email, ct);

        if (user is null)
        {
            hasher.VerifyHashedPassword(null!, DummyHash.Value, req.Password);
            return LoginResult.InvalidCredentials();
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verification == PasswordVerificationResult.Failed)
            return LoginResult.InvalidCredentials();

        var (token, expiresAt) = tokens.CreateAccessToken(user);
        return LoginResult.Success(new LoginResponse(token, expiresAt));
    }
}
