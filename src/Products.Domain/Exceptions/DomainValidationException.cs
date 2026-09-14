namespace Products.Domain.Exceptions;

/// <summary>
/// Raised when an operation would put an aggregate into an invalid state.
/// </summary>
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
