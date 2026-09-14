using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Products.Api.Authentication;
using Products.Api.Infrastructure;
using Products.Api.OpenApi;
using Products.Application;
using Products.Infrastructure;
using Products.Infrastructure.Persistence;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

// A bootstrap logger, active before configuration is read, so a failure during
// start-up is still logged somewhere rather than vanishing.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replaces the bootstrap logger with the fully configured one. Reading the
    // configuration from appsettings means sinks and levels are changed without
    // a rebuild.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Products.Api"));

    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    await app.ConfigurePipelineAsync();

    await app.RunAsync();
    return 0;
}
// HostAbortedException is a normal shutdown. StopTheHostException is internal
// to the hosting infrastructure WebApplicationFactory uses to grab the built
// host; swallowing it breaks every integration test.
catch (Exception ex) when (
    ex is not HostAbortedException &&
    ex.GetType().Name is not "StopTheHostException")
{
    Log.Fatal(ex, "Products API terminated unexpectedly during start-up.");
    return 1;
}
finally
{
    // Flushes buffered log entries. Without this, the logs explaining a crash
    // can be lost precisely when they matter most.
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Service registration and pipeline configuration for the API host.
/// </summary>
internal static class ApiStartup
{
    /// <summary>Registers everything owned by the API layer.</summary>
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptionsWithValidation<JwtOptions>(configuration, JwtOptions.SectionName);
        services.AddOptionsWithValidation<DemoCredentialsOptions>(
            configuration, DemoCredentialsOptions.SectionName);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Enums travel as names ("Red"), not ordinals (1). An ordinal is
                // meaningless to a client and silently changes meaning if a
                // value is ever inserted into the middle of the enum.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

                // Omitting nulls keeps payloads smaller and stops optional
                // fields reading as "explicitly set to null".
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull;
            });

        // Model-binding failures (a malformed Guid in the route, a string where a
        // decimal belongs) never reach a handler, so they bypass the exception
        // handler. This makes those responses ProblemDetails too, so a client
        // parses one error shape rather than two.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] =
                    System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddApiVersioningSupport();
        services.AddSwaggerSupport();
        services.AddJwtAuthentication();
        services.AddApiRateLimiting(configuration);

        // Behind a load balancer, ingress or CDN, the socket's remote address is
        // the proxy's, not the client's. Without this the rate limiter buckets
        // every anonymous user together and request logs record one address for
        // the entire internet.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // KnownIPNetworks/KnownProxies default to loopback only. In a real
            // deployment these must name the actual proxy, otherwise the headers
            // are ignored. Cleared without naming a real proxy, any client can
            // spoof X-Forwarded-For to escape its own rate-limit bucket.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        services.AddCorsPolicy(configuration);

        return services;
    }

    /// <summary>
    /// Binds an options type and validates it at start-up.
    /// </summary>
    private static IServiceCollection AddOptionsWithValidation<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection AddApiVersioningSupport(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);

                // So the unversioned /api/products route from the brief keeps
                // working while /api/v1/products is also available.
                options.AssumeDefaultVersionWhenUnspecified = true;

                // Advertises supported and deprecated versions in response
                // headers, which is how a client learns a version is going away
                // before it is removed.
                options.ReportApiVersions = true;

                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("X-Api-Version"),
                    new QueryStringApiVersionReader("api-version"));
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    private static IServiceCollection AddSwaggerSupport(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.ConfigureOptions<ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            // Surfaces the XML doc comments written throughout the codebase as
            // API documentation, so the two cannot drift apart.
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste only the token value from POST /api/auth/token. " +
                    "Swagger adds the 'Bearer ' prefix itself.",
            });

            // Microsoft.OpenApi v2 (which Swashbuckle 10 builds on) replaced the
            // old Reference/ReferenceType pair with a dedicated reference type.
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme)] = [],
            });

            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // The validation parameters are supplied by ConfigureJwtBearerOptions,
        // which resolves JwtOptions through the options system rather than
        // reading configuration during registration.
        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        services.AddAuthorization();

        return services;
    }

    private static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName));

        services.ConfigureOptions<ConfigureCorsOptions>();
        services.AddCors();

        return services;
    }

    /// <summary>Builds the HTTP pipeline. Middleware order here is behaviour, not style.</summary>
    public static async Task ConfigurePipelineAsync(this WebApplication app)
    {
        // First: rewrites the client address and scheme from the forwarded
        // headers, so logging, rate limiting and HTTPS redirection all see the
        // real client rather than the proxy.
        app.UseForwardedHeaders();

        // Then the exception handler, so it catches everything after it.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // One structured log line per request instead of the framework's several,
        // with the details worth querying on attached as properties.
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserName", httpContext.User.Identity.Name);
                }
            };
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in provider.ApiVersionDescriptions.Reverse())
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        $"Products API {description.GroupName.ToUpperInvariant()}");
                }

                options.DocumentTitle = "Products API";
                options.DisplayRequestDuration();
            });
        }
        else
        {
            // Outside development, redirect to HTTPS and instruct browsers to
            // refuse plain HTTP for this host in future.
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        // CORS must precede authentication: a rejected pre-flight never carries
        // credentials, and the browser needs the CORS headers on it regardless.
        app.UseCors(ConfigureCorsOptions.PolicyName);

        app.UseRateLimiter();

        // Authentication establishes who the caller is; authorisation decides
        // what they may do. This order is required.
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthCheckEndpoints();

        await app.InitialiseDatabaseAsync();
    }

    private static void MapHealthCheckEndpoints(this WebApplication app)
    {
        // Readiness: includes the database probe. An orchestrator uses this to
        // decide whether to send traffic here.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponseAsync,
        }).AllowAnonymous();

        // Liveness: deliberately excludes the database. If the database is down,
        // restarting this process does not help; a failing liveness probe would
        // just crash-loop a healthy service.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponseAsync,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync,
        }).AllowAnonymous();
    }

    /// <summary>Writes health results as JSON rather than the default bare string.</summary>
    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,

                // The exception message is withheld on purpose, /health is
                // anonymous, and a failed database check would otherwise publish
                // connection details to anyone who asks.
                description = entry.Value.Description,
            }),
        });
    }

    private static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var initialiser = scope.ServiceProvider.GetRequiredService<ProductsDbContextInitialiser>();

        await initialiser.InitialiseAsync();

        // Seeding is opt-in via configuration, so a real environment is never
        // populated with demo rows just because the service restarted.
        if (app.Configuration.GetValue("Database:SeedDemoData", defaultValue: false))
        {
            await initialiser.SeedAsync();
        }
    }
}

/// <summary>
/// Exposed so the integration tests can drive the real pipeline through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
