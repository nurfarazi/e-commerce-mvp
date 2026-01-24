namespace ECommerceMvp.Shared.Domain;

/// <summary>
/// Base interface for all domain events.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance (for idempotency).
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// Aggregate ID that this event belongs to.
    /// </summary>
    string AggregateId { get; }

    /// <summary>
    /// Type of event (fully qualified name).
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Version of this event schema (for evolution).
    /// </summary>
    int EventVersion { get; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Base abstract class for domain events with common metadata.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid().ToString();
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public string EventId { get; }
    public string AggregateId { get; set; } = string.Empty;
    public string EventType => GetType().FullName ?? GetType().Name;
    public abstract int EventVersion { get; }
    public DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Envelope for domain events with correlation and causation metadata.
/// </summary>
public class DomainEventEnvelope
{
    public DomainEventEnvelope(
        IDomainEvent @event,
        string correlationId,
        string? causationId = null,
        string? tenantId = null,
        string? userId = null)
    {
        DomainEvent = @event ?? throw new ArgumentNullException(nameof(@event));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        CausationId = causationId;
        IdempotencyKey = causationId; // Use causationId as idempotency key
        TenantId = tenantId;
        UserId = userId;
    }

    public IDomainEvent DomainEvent { get; }
    public string CorrelationId { get; }
    public string? CausationId { get; }
    public string? IdempotencyKey { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}
