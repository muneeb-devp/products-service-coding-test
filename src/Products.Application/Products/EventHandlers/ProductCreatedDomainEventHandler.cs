using MediatR;
using Microsoft.Extensions.Logging;
using Products.Application.Common;
using Products.Application.Common.Abstractions;
using Products.Domain.Products.Events;

namespace Products.Application.Products.EventHandlers;

/// <summary>
/// Reacts to <see cref="ProductCreatedDomainEvent"/>.
/// </summary>
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
