namespace ECommerceMvp.Shared.Domain;

/// <summary>
/// Base interface for aggregate roots.
/// </summary>
public interface IAggregateRoot<TId> where TId : notnull
{
    TId Id { get; }
    int Version { get; }
    IReadOnlyList<IDomainEvent> UncommittedEvents { get; }
    void ClearUncommittedEvents();
}

/// <summary>
/// Abstract base class for aggregate roots with event sourcing support.
/// </summary>
public abstract class AggregateRoot<TId> : IAggregateRoot<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();

    protected AggregateRoot(TId id)
    {
        Id = id;
    }

    public TId Id { get; }
    public int Version { get; protected set; }
    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents()
    {
        _uncommittedEvents.Clear();
    }

    /// <summary>
    /// Append an event to the uncommitted events list.
    /// Called from aggregate behavior methods to record what happened.
    /// </summary>
    protected void AppendEvent(IDomainEvent @event)
    {
        _uncommittedEvents.Add(@event);
        Version++;
    }

    /// <summary>
    /// Replay an event during aggregate reconstruction (loading from event store).
    /// Subclasses override this to apply event state changes.
    /// </summary>
    public virtual void ApplyEvent(IDomainEvent @event)
    {
        // Subclasses implement specific behavior
    }

    /// <summary>
    /// Load all events and rebuild aggregate state (event replay).
    /// </summary>
    public void LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        foreach (var evt in events)
        {
            ApplyEvent(evt);
            Version++;
        }
    }
}

/// <summary>
/// Base class for domain exceptions.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Base class for value objects (immutable, equality by value).
/// </summary>
public abstract class ValueObject
{
    public abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => new { x, y }.GetHashCode())
            .GetHashCode();
    }
}
