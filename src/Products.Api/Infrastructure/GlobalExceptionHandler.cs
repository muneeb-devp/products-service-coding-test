using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Products.Application.Common.Exceptions;
using Products.Domain.Exceptions;
using ValidationException = Products.Application.Common.Exceptions.ValidationException;

namespace Products.Api.Infrastructure;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 <c>ProblemDetails</c> responses.
/// </summary>
/// <remarks>
/// Implemented as <see cref="IExceptionHandler"/> rather than hand-written
/// middleware. It is the framework's supported extension point since .NET 8,
/// participates in <c>AddProblemDetails</c> so every error response is shaped
/// consistently, and it keeps the mapping table in one readable place instead of
/// spread through try/catch blocks in controllers.
/// <para>
/// The key security property: only <em>known</em> exception types have their
/// message copied into the response. Anything unrecognised becomes a generic
/// "An unexpected error occurred", because exception messages routinely contain
/// connection strings, file paths and schema details. The real detail goes to
/// the log, correlated by trace id, where it is useful without being public.
/// </para>
/// </remarks>
internal sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Converted once, up front: passing httpContext.Request.Path directly to a
        // log method would run the PathString conversion on every call, including
        // when the level is disabled.
        var requestPath = httpContext.Request.Path.Value ?? "/";

        // A cancelled request is the client hanging up, not a server fault.
        // Logging it as an error trains people to ignore errors, and there is
        // no connection left to write a response to.
        if (exception is OperationCanceledException or TaskCanceledException
            && httpContext.RequestAborted.IsCancellationRequested)
        {
            ApiLog.RequestAborted(logger, requestPath);
            return true;
        }

        var problemDetails = MapToProblemDetails(exception, httpContext);

        // Correlates the opaque client-facing response with the full detail in
        // the logs. A user can quote this id in a support ticket.
        problemDetails.Extensions["traceId"] =
            System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
        {
            ApiLog.UnhandledException(logger, exception, requestPath);
        }
        else
        {
            ApiLog.HandledRequestFault(
                logger, requestPath, problemDetails.Status ?? 0, exception.Message);
        }

        httpContext.Response.StatusCode =
            problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static ProblemDetails MapToProblemDetails(Exception exception, HttpContext context) =>
        exception switch
        {
            // Application-layer validation: the full per-field failure list.
            ValidationException validation => new ValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Instance = context.Request.Path,
            },

            // Domain guards. Reaching one means input bypassed a validator, so
            // it is still the caller's fault — 400, not 500.
            DomainValidationException domain => new ValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [domain.PropertyName ?? "request"] = [domain.Message],
                })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Instance = context.Request.Path,
            },

            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFound.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Instance = context.Request.Path,
            },

            // Well-formed request, but it conflicts with current state — a
            // duplicate SKU. 400 would wrongly suggest a malformed payload.
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflict.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                Instance = context.Request.Path,
            },

            // Everything else: deliberately opaque. exception.Message here would
            // be an information-disclosure bug.
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "An unexpected error occurred while processing your request.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Instance = context.Request.Path,
            },
        };
}
