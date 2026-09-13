using Products.Domain.Common;

namespace Products.Domain.Products.Events;

/// <summary>
/// Raised when a new product has been created.
/// </summary>
/// <remarks>
/// In this codebase the event is dispatched in-process after the transaction
/// commits. In the wider architecture (see <c>docs/architecture.md</c>) this is
/// exactly the hook that would translate into a <c>ProductCreated</c> integration
/// event published to the broker for Orders and Search to consume.
/// </remarks>
/// <param name="ProductId">Identity of the product that was created.</param>
/// <param name="Sku">The product's business identifier.</param>
/// <param name="Name">The product's name at creation time.</param>
/// <param name="Colour">The product's colour at creation time.</param>
/// <param name="OccurredOn">When the creation happened (UTC).</param>
public sealed record ProductCreatedDomainEvent(
    Guid ProductId,
    string Sku,
    string Name,
    ProductColour Colour,
    DateTimeOffset OccurredOn) : IDomainEvent;
