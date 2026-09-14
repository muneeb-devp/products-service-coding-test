using MediatR;
using Products.Application.Common.Abstractions;
using Products.Domain.Common;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Publishes domain events through MediatR, wrapping each one in a
/// <see cref="DomainEventNotification{TDomainEvent}"/>.
/// </summary>
internal sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type>
        NotificationTypeCache = new();

    /// <inheritdoc />
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = NotificationTypeCache.GetOrAdd(
                domainEvent.GetType(),
                eventType => typeof(DomainEventNotification<>).MakeGenericType(eventType));

            var notification = (INotification)Activator.CreateInstance(
                notificationType, domainEvent)!;

            await publisher.Publish(notification, cancellationToken);
        }
    }
}
