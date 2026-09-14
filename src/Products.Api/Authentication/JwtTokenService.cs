using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Products.Api.Authentication;

/// <summary>
/// Issues short-lived HS256 JWTs.
/// </summary>
public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) CreateToken(
        string subject,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.TokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),

            // A unique token id, so an individual token could be denylisted
            // without invalidating every token ever issued.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // Issued-at as Unix seconds, which is what the spec requires.
            new(JwtRegisteredClaimNames.Iat,
                now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        };

        if (additionalClaims is not null)
        {
            claims.AddRange(additionalClaims);
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
