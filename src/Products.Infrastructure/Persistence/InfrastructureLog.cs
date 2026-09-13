using Microsoft.Extensions.Logging;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Source-generated log messages for the Infrastructure layer.
/// </summary>
/// <remarks>
/// Same rationale as the Application layer's equivalent: templates are verified
/// against their arguments at compile time, and nothing is evaluated when the
/// level is disabled.
/// </remarks>
internal static partial class InfrastructureLog
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "Database initialisation failed.")]
    public static partial void DatabaseInitialisationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Seeded {ProductCount} demo products.")]
    public static partial void SeededProducts(this ILogger logger, int productCount);
}
