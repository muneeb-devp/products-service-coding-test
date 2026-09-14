using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Products.Api.Authentication;

/// <summary>
/// Configures JWT bearer validation from <see cref="JwtOptions"/>.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(
    IOptions<JwtOptions> jwtOptions,
    IWebHostEnvironment environment) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name is not JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Every one of these is on deliberately. Turning any of them off is
            // the usual way a JWT implementation becomes decorative: an
            // unvalidated issuer or audience means a token minted for a
            // different service is accepted here.
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = _jwt.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),

            ValidateLifetime = true,

            // Default is five minutes, which silently extends every token's
            // life. Thirty seconds absorbs real clock drift without
            // meaningfully widening the window.
            ClockSkew = TimeSpan.FromSeconds(30),

            // Pin the algorithm. Without this the token's own header influences
            // how it is verified, which is the root of the classic "alg"
            // confusion attacks.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };

        // Bearer tokens are credentials: only ever over TLS outside development.
        options.RequireHttpsMetadata = !environment.IsDevelopment();
    }
}
