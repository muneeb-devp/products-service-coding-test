using System.ComponentModel.DataAnnotations;

namespace Products.Api.Authentication;

/// <summary>
/// Settings for issuing and validating JWT bearer tokens.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Minimum signing key length in bytes.
    /// </summary>
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
    [Range(1, 1440)]
    public int TokenLifetimeMinutes { get; init; } = 60;
}
