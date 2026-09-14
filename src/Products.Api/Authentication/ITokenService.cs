using System.Security.Claims;

namespace Products.Api.Authentication;

/// <summary>Issues bearer tokens for the demo authentication endpoint.</summary>
public interface ITokenService
{
    /// <summary>
    /// Issues a signed JWT for the given subject.
    /// </summary>
    /// <param name="subject">The principal the token represents.</param>
    /// <param name="additionalClaims">Extra claims to embed, such as roles.</param>
    /// <returns>The compact token and the instant it expires.</returns>
    (string Token, DateTimeOffset ExpiresAt) CreateToken(
        string subject,
        IEnumerable<Claim>? additionalClaims = null);
}
