using System.ComponentModel.DataAnnotations;

namespace Products.Api.Authentication;

/// <summary>
/// Credentials accepted by the demo token endpoint.
/// </summary>
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
