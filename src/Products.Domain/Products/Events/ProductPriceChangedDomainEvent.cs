using Products.Domain.Common;

namespace Products.Domain.Products.Events;

/// <summary>
/// Raised when a product's price changes, carrying both the old and new value.
/// </summary>
public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    string Currency,
    DateTimeOffset OccurredOn) : IDomainEvent;
