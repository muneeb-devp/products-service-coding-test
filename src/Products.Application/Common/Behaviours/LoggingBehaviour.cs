using MediatR;
using Microsoft.Extensions.Logging;
using Products.Application.Common;

namespace Products.Application.Common.Behaviours;

/// <summary>
/// Emits a structured log entry for every request entering and leaving the
/// Application layer.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.HandlingRequest(requestName);

        try
        {
            var response = await next(cancellationToken);
            logger.HandledRequest(requestName);
            return response;
        }
        catch (Exception ex)
        {
            // Log and rethrow: the global exception handler owns the HTTP
            // response, this only makes sure the failure is attributed to the
            // specific request that caused it.
            logger.RequestFailed(ex, requestName, ex.Message);
            throw;
        }
    }
}
