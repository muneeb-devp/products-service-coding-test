using MediatR;
using Microsoft.Extensions.Logging;
using Products.Application.Common;
using Products.Application.Common.Abstractions;
using Products.Domain.Products.Events;

namespace Products.Application.Products.EventHandlers;

/// <summary>
/// Reacts to <see cref="ProductPriceChangedDomainEvent"/>.
/// </summary>
/// <remarks>
/// A price change is the event other bounded contexts most want: Orders needs it
/// to decide whether a basket still reflects current pricing, and Payments needs
/// it for reconciliation. See <c>docs/architecture.md</c>.
/// </remarks>
public sealed class ProductPriceChangedDomainEventHandler(
    ILogger<ProductPriceChangedDomainEventHandler> logger)
    : INotificationHandler<DomainEventNotification<ProductPriceChangedDomainEvent>>
{
    /// <inheritdoc />
    public Task Handle(
        DomainEventNotification<ProductPriceChangedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        logger.ProductPriceChanged(
            domainEvent.ProductId,
            domainEvent.OldPrice,
            domainEvent.NewPrice,
            domainEvent.Currency);

        return Task.CompletedTask;
    }
}
