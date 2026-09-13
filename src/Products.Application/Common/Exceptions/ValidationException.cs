namespace Products.Application.Common.Exceptions;

/// <summary>
/// Aggregates one or more validation failures produced by the MediatR
/// validation behaviour. Mapped to an RFC 7807 <c>ValidationProblemDetails</c>
/// (HTTP 400) by the global exception handler.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : this() => Errors = errors;

    /// <summary>
    /// Failures keyed by property name. The shape matches what
    /// <c>ValidationProblemDetails</c> expects, so the API layer can hand it
    /// straight to the client without reshaping.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }
}
