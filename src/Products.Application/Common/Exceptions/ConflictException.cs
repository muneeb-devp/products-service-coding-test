namespace Products.Application.Common.Exceptions;

/// <summary>
/// Thrown when a request is well-formed but conflicts with the current state of
/// the resource, for example, creating a product with a SKU that is already in
/// use. Mapped to HTTP 409 by the global exception handler.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
