using MediatR;
using Products.Domain.Common;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// Adapts a Domain-layer <see cref="IDomainEvent"/> to a MediatR
/// <see cref="INotification"/> so it can be dispatched in-process.
/// </summary>
/// <remarks>
/// This wrapper is the reason <see cref="IDomainEvent"/> carries no MediatR
/// reference. The domain records that something happened; this Application-layer
/// adapter decides that MediatR is how it gets delivered. Swapping MediatR out,
/// or publishing to a broker instead, changes this file and nothing in the
/// domain.
/// </remarks>
/// <typeparam name="TDomainEvent">The concrete domain event type.</typeparam>
/// <param name="DomainEvent">The event being delivered.</param>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent)
    : INotification
    where TDomainEvent : IDomainEvent;
