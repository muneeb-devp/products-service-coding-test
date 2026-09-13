using Products.Domain.Common;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// Publishes domain events raised by aggregates during a unit of work.
/// </summary>
/// <remarks>
/// Called by the persistence layer <em>after</em> <c>SaveChanges</c> succeeds,
/// so a handler can never react to a change that is subsequently rolled back.
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>Publishes each event to its registered handlers.</summary>
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
