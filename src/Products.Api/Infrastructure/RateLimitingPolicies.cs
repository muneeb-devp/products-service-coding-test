using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Products.Api.Infrastructure;

/// <summary>
/// Named rate-limiting policies and their registration.
/// </summary>
internal static class RateLimitingPolicies
{
    /// <summary>Policy protecting write endpoints (POST, PUT, DELETE).</summary>
    public const string Writes = "writes";

    /// <summary>Policy protecting the token endpoint.</summary>
    public const string Authentication = "authentication";

    /// <summary>Registers the rate-limiting policies.</summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddRateLimiter(options =>
        {
            // 429 is the correct status; the default is 503, which wrongly
            // suggests the service is down rather than that this caller is
            // going too fast.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(Writes, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Partitioned per authenticated user, falling back to remote
                    // IP for anonymous callers. A single global limiter would
                    // let one noisy client lock out everyone else.
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = GetLimits(httpContext).Writes.PermitLimit,
                        Window = TimeSpan.FromSeconds(GetLimits(httpContext).Writes.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,

                        // No queue: a write held in a queue is a request the
                        // client thinks is in flight. Rejecting immediately lets
                        // it retry with backoff instead of holding a connection.
                        QueueLimit = 0,
                    }));

            options.AddPolicy(Authentication, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        // Tighter: this endpoint is what a credential-stuffing
                        // attempt would hammer.
                        PermitLimit = GetLimits(httpContext).Authentication.PermitLimit,
                        Window = TimeSpan.FromSeconds(
                            GetLimits(httpContext).Authentication.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));

            // Tell the client how long to wait instead of leaving it to guess.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsync(
                    """
                    {"type":"https://tools.ietf.org/html/rfc6585#section-4",
                     "title":"Too many requests",
                     "status":429,
                     "detail":"Rate limit exceeded. Please retry shortly."}
                    """,
                    cancellationToken);
            };
        });
    }

    /// <summary>
    /// Resolves the configured limits from the request's service scope.
    /// </summary>
    private static RateLimitingOptions GetLimits(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    /// <summary>
    /// Chooses the bucket a request is counted against.
    /// </summary>
    private static string GetPartitionKey(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? $"user:{httpContext.User.Identity.Name}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
