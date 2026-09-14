using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Products.Api.Infrastructure;

/// <summary>Origins permitted to call the API from a browser.</summary>
public sealed class CorsSettings
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// Exact origins allowed, e.g. <c>https://app.example.com</c>.
    /// </summary>
    /// <remarks>
    /// An empty list means no cross-origin access, not "allow everything". A
    /// permissive default is how a development convenience reaches production.
    /// </remarks>
    public string[] AllowedOrigins { get; init; } = [];
}

/// <summary>
/// Builds the CORS policy from <see cref="CorsSettings"/>.
/// </summary>
/// <remarks>
/// Configured through the options system for the same reason as JWT bearer: so
/// the origins come from the fully composed configuration rather than from
/// whatever was loaded at registration time.
/// </remarks>
internal sealed class ConfigureCorsOptions(IOptions<CorsSettings> settings)
    : IConfigureOptions<CorsOptions>
{
    /// <summary>Name of the policy applied to the pipeline.</summary>
    public const string PolicyName = "FrontendOrigins";

    public void Configure(CorsOptions options) =>
        options.AddPolicy(PolicyName, policy =>
        {
            var origins = settings.Value.AllowedOrigins;

            if (origins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Lets the browser read the paging/versioning headers the API
                // sets; without this they are hidden from JavaScript.
                .WithExposedHeaders(
                    "api-supported-versions", "api-deprecated-versions", "Retry-After");
        });
}
