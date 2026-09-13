namespace Products.Application.Common.Exceptions;

/// <summary>
/// Thrown when a request is well-formed but conflicts with the current state of
/// the resource — for example, creating a product with a SKU that is already in
/// use. Mapped to HTTP 409 by the global exception handler.
/// </summary>
/// <remarks>
/// This is deliberately distinct from a validation failure. The request is not
/// malformed, so 400 would mislead the client into "fix your payload" when the
/// correct action is "pick a different SKU, or update the existing product".
/// </remarks>
public sealed class ConflictException(string message) : Exception(message);
