using Products.Domain.Common;

namespace Products.Domain.Products.Events;

/// <summary>
/// Raised when a product's price changes, carrying both the old and new value.
/// </summary>
/// <remarks>
/// Carrying the previous price matters: a consumer such as a pricing-history or
/// notification service needs the delta, and it cannot reconstruct it from the
/// new state alone. This is the difference between an event ("the price changed
/// from X to Y") and a state notification ("the price is now Y").
/// </remarks>
public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    string Currency,
    DateTimeOffset OccurredOn) : IDomainEvent;
