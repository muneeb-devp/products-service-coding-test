using System.ComponentModel.DataAnnotations;

namespace Products.Api.Infrastructure;

/// <summary>
/// Tunable limits for the rate-limiting policies.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Limits applied to write endpoints (POST, PUT, DELETE).</summary>
    public RateLimitPolicyOptions Writes { get; init; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60,
    };

    /// <summary>
    /// Limits applied to the token endpoint.
    /// </summary>
    public RateLimitPolicyOptions Authentication { get; init; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60,
    };
}

/// <summary>Fixed-window limit for a single policy.</summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>Requests allowed per window, per partition.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; }

    /// <summary>Window length in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; }
}
