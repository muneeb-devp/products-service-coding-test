using MediatR;
using Microsoft.Extensions.Logging;
using Products.Application.Common;
using Products.Application.Common.Abstractions;
using Products.Domain.Products.Events;

namespace Products.Application.Products.EventHandlers;

/// <summary>
/// Reacts to <see cref="ProductCreatedDomainEvent"/>.
/// </summary>
/// <remarks>
/// This handler is the seam where the service would join the event-driven
/// architecture described in <c>docs/architecture.md</c>. Today it logs; in
/// production the body would write a <c>ProductCreated</c> integration event to
/// a transactional outbox for a relay to publish to the broker.
/// <para>
/// The outbox matters and is not an implementation detail: publishing directly
/// from here would be a dual write. If the broker call fails after the database
/// commit, the product exists and no one downstream ever hears about it — the
/// classic silent inconsistency in event-driven systems. Writing the event to
/// the same database, in the same transaction, and relaying it separately is
/// what makes "saved" and "announced" succeed or fail together.
/// </para>
/// </remarks>
public sealed class ProductCreatedDomainEventHandler(
    ILogger<ProductCreatedDomainEventHandler> logger)
    : INotificationHandler<DomainEventNotification<ProductCreatedDomainEvent>>
{
    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<ProductCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        logger.ProductCreated(
            domainEvent.ProductId, domainEvent.Sku, domainEvent.Name, domainEvent.Colour);

        return Task.CompletedTask;
    }
}
