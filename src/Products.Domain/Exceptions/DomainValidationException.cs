namespace Products.Domain.Exceptions;

/// <summary>
/// Raised when an operation would put an aggregate into an invalid state.
/// </summary>
/// <remarks>
/// This is the domain's own guard rail and is deliberately independent of
/// FluentValidation, which guards the <em>application</em> boundary. Input that
/// arrives over HTTP is rejected by a validator long before it reaches here, so
/// a <see cref="DomainValidationException"/> in production signals a genuine bug
/// (a code path that bypassed validation) rather than bad user input.
/// The API maps it to HTTP 400 all the same, so it can never leak as a 500.
/// </remarks>
public sealed class DomainValidationException : Exception
{
    /// <summary>The name of the member that failed validation, when known.</summary>
    public string? PropertyName { get; }

    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string propertyName, string message)
        : base(message)
    {
        PropertyName = propertyName;
    }
}
