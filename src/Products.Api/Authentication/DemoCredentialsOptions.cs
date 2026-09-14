using System.ComponentModel.DataAnnotations;

namespace Products.Api.Authentication;

/// <summary>
/// Credentials accepted by the demo token endpoint.
/// </summary>
/// <remarks>
/// Bound from configuration rather than hardcoded. A password compiled into the
/// binary is a back door that survives every redeployment and cannot be rotated;
/// this at least can be overridden per environment, and in a real system the
/// whole type disappears along with the demo endpoint.
/// </remarks>
public sealed class DemoCredentialsOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "DemoCredentials";

    /// <summary>The accepted username.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = string.Empty;

    /// <summary>The accepted password.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;
}
