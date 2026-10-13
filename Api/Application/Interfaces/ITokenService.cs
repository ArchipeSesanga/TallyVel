using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Api.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Api.Application.Interfaces;
public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _o = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_o.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.IsPlatformAdmin)
            claims.Add(new Claim("role", "PlatformAdmin"));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}