using Microsoft.Extensions.Logging;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Source-generated log messages for the Infrastructure layer.
/// </summary>
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
