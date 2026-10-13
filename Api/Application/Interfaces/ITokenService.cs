using Domain;

namespace Api.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);

    /// <summary>A new random refresh token: the raw value to give the client, and the hash to store.</summary>
    (string RawToken, string Hash) CreateRefreshToken();
}
