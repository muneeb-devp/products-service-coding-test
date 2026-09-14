using Products.Domain.Common;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// Publishes domain events raised by aggregates during a unit of work.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Publishes each event to its registered handlers.</summary>
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
