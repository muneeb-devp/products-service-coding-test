using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Products.Api.Infrastructure;

/// <summary>
/// Named rate-limiting policies and their registration.
/// </summary>
/// <remarks>
/// Limits are applied to the endpoints that need them rather than globally.
/// Reads are cheap and idempotent; writes mutate state and cost a database round
/// trip, and the token endpoint is the natural target for credential stuffing.
/// Throttling reads at the same rate would degrade the frontend for no gain.
/// </remarks>
internal static class RateLimitingPolicies
{
    /// <summary>Policy protecting write endpoints (POST, PUT, DELETE).</summary>
    public const string Writes = "writes";

    /// <summary>Policy protecting the token endpoint.</summary>
    public const string Authentication = "authentication";

    /// <summary>Registers the rate-limiting policies.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
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
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
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
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
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

    private static string GetPartitionKey(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? $"user:{httpContext.User.Identity.Name}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
