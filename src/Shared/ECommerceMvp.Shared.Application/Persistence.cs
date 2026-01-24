using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Shared.Application;

/// <summary>
/// Repository interface for loading and saving aggregates.
/// </summary>
public interface IRepository<TAggregate, TId> where TAggregate : IAggregateRoot<TId> where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event store interface for persisting and loading events.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Append events to an aggregate's event stream with optimistic concurrency check.
    /// </summary>
    Task AppendAsync(
        string streamId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        string correlationId,
        string? causationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all events from an aggregate's stream.
    /// </summary>
    Task<IEnumerable<IDomainEvent>> LoadStreamAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all events from a specific version onwards.
    /// </summary>
    Task<IEnumerable<IDomainEvent>> LoadStreamFromVersionAsync(
        string streamId,
        int fromVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Event publisher interface for publishing committed events to message broker.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(
        IEnumerable<DomainEventEnvelope> eventEnvelopes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Idempotency store interface for tracking processed commands and events.
/// </summary>
public interface IIdempotencyStore
{
    Task<bool> IsCommandProcessedAsync(string commandId, CancellationToken cancellationToken = default);
    Task<T?> GetCommandResultAsync<T>(string commandId, CancellationToken cancellationToken = default);
    Task MarkCommandAsProcessedAsync(string commandId, object result, CancellationToken cancellationToken = default);

    Task<bool> IsEventProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default);
    Task MarkEventAsProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default);

    Task<IdempotencyCheckResult> CheckIdempotencyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task MarkIdempotencyProcessedAsync(string idempotencyKey, string aggregateId, CancellationToken cancellationToken = default);
}

public class IdempotencyCheckResult
{
    public bool IsIdempotent { get; set; }
    public string AggregateId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Exception thrown when optimistic concurrency conflict occurs.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
/// <summary>
/// In-memory event publisher for MVP - logs events without actual message broker.
/// In production, replace with RabbitMQ, Kafka, Azure Service Bus, etc.
/// </summary>
public class InMemoryEventPublisher : IEventPublisher
{
    private readonly List<DomainEventEnvelope> _publishedEvents = [];

    public async Task PublishAsync(
        IEnumerable<DomainEventEnvelope> eventEnvelopes,
        CancellationToken cancellationToken = default)
    {
        foreach (var envelope in eventEnvelopes)
        {
            _publishedEvents.Add(envelope);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Get all published events (for testing/debugging).
    /// </summary>
    public IReadOnlyList<DomainEventEnvelope> GetPublishedEvents() => _publishedEvents.AsReadOnly();
}
