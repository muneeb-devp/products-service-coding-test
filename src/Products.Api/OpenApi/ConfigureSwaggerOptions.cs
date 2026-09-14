using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Products.Api.OpenApi;

/// <summary>
/// Generates one Swagger document per discovered API version.
/// </summary>
internal sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfo(description));
        }
    }

    private static OpenApiInfo CreateInfo(ApiVersionDescription description)
    {
        var info = new OpenApiInfo
        {
            Title = "Products API",
            Version = description.ApiVersion.ToString(),
            Description =
                """
                Product catalogue service.

                **Authentication.** Every `/api/products` endpoint requires a bearer token.
                To obtain one, call `POST /api/auth/token` with the demo credentials
                (`demo` / `Password123!` by default), then click **Authorize** above and
                paste the `accessToken` value.

                The token endpoint is a stand-in for a real identity provider so the
                secured endpoints can be exercised without external infrastructure.
                """,
            Contact = new OpenApiContact { Name = "Muneeb Mughal" },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT"),
            },
        };

        if (description.IsDeprecated)
        {
            info.Description += "\n\n**This API version has been deprecated.**";
        }

        return info;
    }
}
