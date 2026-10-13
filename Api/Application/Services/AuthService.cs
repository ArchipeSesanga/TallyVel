using Api.Application.Contracts;
using Api.Application.Interfaces;
using Api.Common;
using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Api.Application.Services;

public class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    // One message for every login failure, so the wording can't drift apart.
    public const string InvalidCredentials = "Invalid email or password.";
    public const string InvalidSession = "Your session is invalid or has expired. Please sign in again.";

    // Verified against this when the email is unknown, so a missing account
    // takes about as long to reject as a wrong password.
    // Computed once per process, not per request.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString()));

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

    public async Task<AuthResult> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var email = EmailNormalizer.Normalize(req.Email);
        var user = await users.GetByEmailAsync(email, ct);

        if (user is null)
        {
            hasher.VerifyHashedPassword(null!, DummyHash.Value, req.Password); // burn the same CPU time
            return AuthResult.Unauthorized(InvalidCredentials);
        }

        var check = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (check == PasswordVerificationResult.Failed)
            return AuthResult.Unauthorized(InvalidCredentials);

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(hasher.HashPassword(user, req.Password));
            await users.UpdateAsync(user, ct);
        }

        // A login starts a new family of refresh tokens.
        return await IssueTokensAsync(user, familyId: Guid.NewGuid(), ct);
    }

    public async Task<AuthResult> RefreshAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawToken))
            return AuthResult.Unauthorized(InvalidSession);

        var now = clock.GetUtcNow();
        var existing = await refreshTokens.GetByHashAsync(TokenService.Hash(rawToken), ct);

        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= now)
            return AuthResult.Unauthorized(InvalidSession);

        if (existing.UsedAt is not null)
        {
            // REUSE: someone presented a token that was already exchanged. Kill the whole family.
            await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
            logger.LogWarning("Refresh token reuse detected for user {UserId}, family {FamilyId} revoked",
                existing.UserId, existing.FamilyId);
            return AuthResult.Unauthorized(InvalidSession);
        }

        var newId = Guid.NewGuid();

        // "Mark old as used" and "insert new" succeed or fail together.
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);

        if (!await refreshTokens.TryMarkUsedAsync(existing.Id, newId, now, ct))
        {
            // Lost a race with a concurrent request using the same token: that is also reuse
            await refreshTokens.RevokeFamilyAsync(existing.FamilyId, now, ct);
            await tx.CommitAsync(ct);
            logger.LogWarning("Concurrent refresh token reuse for user {UserId}, family {FamilyId} revoked",
                existing.UserId, existing.FamilyId);
            return AuthResult.Unauthorized(InvalidSession);
        }

        var user = await users.GetByIdAsync(existing.UserId, ct);
        if (user is null)
            return AuthResult.Unauthorized(InvalidSession); // not committed: the transaction rolls back

        var result = await IssueTokensAsync(user, existing.FamilyId, ct, newId);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<AuthResult> IssueTokensAsync(User user, Guid familyId, CancellationToken ct, Guid? newId = null)
    {
        var now = clock.GetUtcNow();
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var (raw, hash) = tokens.CreateRefreshToken();

        var refresh = new RefreshToken
        {
            Id = newId ?? Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays)
        };
        await refreshTokens.AddAsync(refresh, ct);

        return AuthResult.Success(access, accessExpires, raw, refresh.ExpiresAt);
    }
}
