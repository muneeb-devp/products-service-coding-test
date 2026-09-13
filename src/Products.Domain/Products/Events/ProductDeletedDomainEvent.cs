using Products.Domain.Common;

namespace Products.Domain.Products.Events;

/// <summary>
/// Raised when a product has been removed from the catalogue.
/// </summary>
public sealed record ProductDeletedDomainEvent(
    Guid ProductId,
    string Sku,
    DateTimeOffset OccurredOn) : IDomainEvent;
