namespace Products.Domain.Common;

/// <summary>
/// Base class for entities identified by a <see cref="Guid"/>, with support for
/// recording domain events.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>The entity's stable identity.</summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Domain events raised but not yet dispatched. Exposed as a read-only view
    /// so callers cannot mutate the aggregate's event log directly.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records an event to be dispatched once the change is persisted.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clears the pending event log. Called by the dispatcher after delivery.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        // Entity equality is identity equality: two rows with the same Id are the
        // same thing regardless of how their other fields currently differ.
        if (obj is not Entity other || obj.GetType() != GetType())
        {
            return false;
        }

        // A transient entity (default Id) is only ever equal to itself.
        return Id != Guid.Empty && other.Id != Guid.Empty && Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
