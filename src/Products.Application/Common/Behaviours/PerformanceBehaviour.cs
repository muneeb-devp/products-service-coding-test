using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Products.Application.Common;

namespace Products.Application.Common.Behaviours;

/// <summary>
/// Warns when a request takes longer than the acceptable threshold.
/// </summary>
public sealed class PerformanceBehaviour<TRequest, TResponse>(
    ILogger<PerformanceBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Requests slower than this are logged as warnings.</summary>
    public const int LongRunningThresholdMilliseconds = 500;

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timestamp = Stopwatch.GetTimestamp();

        var response = await next(cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(timestamp);

        if (elapsed.TotalMilliseconds > LongRunningThresholdMilliseconds)
        {
            logger.LongRunningRequest(
                typeof(TRequest).Name,
                (long)elapsed.TotalMilliseconds,
                LongRunningThresholdMilliseconds);
        }

        return response;
    }
}
