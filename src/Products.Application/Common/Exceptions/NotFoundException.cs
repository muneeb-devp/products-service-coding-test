namespace Products.Application.Common.Exceptions;

/// <summary>
/// Thrown when an operation targets an entity that does not exist.
/// Mapped to HTTP 404 by the global exception handler.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    /// <summary>The type of entity that was missing, when known.</summary>
    public string? EntityName { get; }

    /// <summary>The key that was looked up, when known.</summary>
    public object? Key { get; }
}
