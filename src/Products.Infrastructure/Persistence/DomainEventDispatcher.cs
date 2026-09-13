using MediatR;
using Products.Application.Common.Abstractions;
using Products.Domain.Common;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Publishes domain events through MediatR, wrapping each one in a
/// <see cref="DomainEventNotification{TDomainEvent}"/>.
/// </summary>
/// <remarks>
/// The wrapping is done reflectively because the concrete event type is only
/// known at run time, and MediatR resolves handlers by the notification's
/// closed generic type. Publishing the bare <see cref="IDomainEvent"/> would
/// match nothing, because a handler is registered against
/// <c>DomainEventNotification&lt;ProductCreatedDomainEvent&gt;</c>, not against
/// the interface.
/// <para>
/// The constructed generic types are cached: <see cref="Type.MakeGenericType"/>
/// is not free, and this runs on every write.
/// </para>
/// </remarks>
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
