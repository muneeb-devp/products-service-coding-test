namespace Products.Api.Infrastructure;

/// <summary>Source-generated log messages for the API layer.</summary>
internal static partial class ApiLog
{
    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Error,
        Message = "Unhandled exception processing {RequestPath}")]
    public static partial void UnhandledException(
        ILogger logger, Exception exception, string requestPath);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Request {RequestPath} rejected with {StatusCode}: {ErrorMessage}")]
    public static partial void HandledRequestFault(
        ILogger logger, string requestPath, int statusCode, string errorMessage);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Debug,
        Message = "Request {RequestPath} was aborted by the client.")]
    public static partial void RequestAborted(ILogger logger, string requestPath);
}
