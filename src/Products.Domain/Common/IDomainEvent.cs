namespace Products.Domain.Common;

/// <summary>
/// A fact that has already happened inside the domain.
/// </summary>
/// <remarks>
/// Domain events are raised by aggregates and dispatched <em>after</em> the
/// transaction commits, so a handler never observes state that might still be
/// rolled back. This is the natural seam where an in-process domain event is
/// translated into an integration event published to a broker — see
/// <c>docs/architecture.md</c>.
/// <para>
/// Note this interface lives in the Domain layer and carries no MediatR
/// dependency: the domain raises events, but knows nothing about who delivers
/// them. The Application layer adapts these to MediatR notifications.
/// </para>
/// </remarks>
public interface IDomainEvent
{
    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredOn { get; }
}
