using Microsoft.Extensions.Logging;
using Products.Domain.Products;

namespace Products.Application.Common;

/// <summary>
/// Source-generated log messages for the Application layer.
/// </summary>
internal static partial class ApplicationLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Handling {RequestName}")]
    public static partial void HandlingRequest(this ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Handled {RequestName}")]
    public static partial void HandledRequest(this ILogger logger, string requestName);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "{RequestName} failed: {ErrorMessage}")]
    public static partial void RequestFailed(
        this ILogger logger, Exception exception, string requestName, string errorMessage);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Long-running request {RequestName} took {ElapsedMilliseconds}ms " +
                  "(threshold {ThresholdMilliseconds}ms)")]
    public static partial void LongRunningRequest(
        this ILogger logger,
        string requestName,
        long elapsedMilliseconds,
        int thresholdMilliseconds);

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Product created: {ProductId} ({Sku}) '{ProductName}' in {Colour}. " +
                  "In production this would enqueue a ProductCreated integration event.")]
    public static partial void ProductCreated(
        this ILogger logger, Guid productId, string sku, string productName, ProductColour colour);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Product {ProductId} price changed from {OldPrice} to {NewPrice} {Currency}.")]
    public static partial void ProductPriceChanged(
        this ILogger logger, Guid productId, decimal oldPrice, decimal newPrice, string currency);
}
