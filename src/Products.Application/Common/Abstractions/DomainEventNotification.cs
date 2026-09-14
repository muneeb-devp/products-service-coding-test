using MediatR;
using Products.Domain.Common;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// Adapts a Domain-layer <see cref="IDomainEvent"/> to a MediatR
/// <see cref="INotification"/> so it can be dispatched in-process.
/// </summary>
/// <typeparam name="TDomainEvent">The concrete domain event type.</typeparam>
/// <param name="DomainEvent">The event being delivered.</param>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent)
    : INotification
    where TDomainEvent : IDomainEvent;
