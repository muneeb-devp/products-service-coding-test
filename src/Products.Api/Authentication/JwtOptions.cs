using System.ComponentModel.DataAnnotations;

namespace Products.Api.Authentication;

/// <summary>
/// Settings for issuing and validating JWT bearer tokens.
/// </summary>
/// <remarks>
/// Bound with <c>ValidateOnStart</c>, so a missing or too-short signing key
/// stops the application at boot rather than surfacing later as tokens that
/// cannot be validated. A security misconfiguration should be a failed
/// deployment, not a runtime surprise.
/// </remarks>
public sealed class JwtOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Minimum signing key length in bytes.
    /// </summary>
    /// <remarks>
    /// HMAC-SHA256 requires a key at least as long as its output (256 bits =
    /// 32 bytes). A shorter key weakens the signature, and the JWT library
    /// rejects it outright, so it is validated here where the error is legible.
    /// </remarks>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>Token issuer (<c>iss</c> claim).</summary>
    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Intended audience (<c>aud</c> claim).</summary>
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric signing key. Supplied via configuration, never committed.</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(MinimumSigningKeyBytes)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Token lifetime in minutes.
    /// </summary>
    /// <remarks>
    /// Deliberately short. These tokens carry no revocation mechanism, so a
    /// leaked one stays valid until it expires — the lifetime <em>is</em> the
    /// blast radius.
    /// </remarks>
    [Range(1, 1440)]
    public int TokenLifetimeMinutes { get; init; } = 60;
}
