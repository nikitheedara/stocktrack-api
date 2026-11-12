using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StockTrack.Api.Domain;

namespace StockTrack.Api.Auth;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.ExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        // Claims are kept to the minimum: an id and a role. The email isn't put in the token
        // because JWTs are only signed, not encrypted, and get copied into logs and browser storage.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("role", user.Role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
