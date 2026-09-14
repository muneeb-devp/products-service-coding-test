namespace Products.Domain.Common;

/// <summary>
/// A fact that has already happened inside the domain.
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredOn { get; }
}
