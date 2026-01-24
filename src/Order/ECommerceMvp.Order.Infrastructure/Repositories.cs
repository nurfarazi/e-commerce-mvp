using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Order.Infrastructure;

/// <summary>
/// In-memory repository implementation for Order aggregate.
/// In production, this would be replaced with a database-backed repository.
/// </summary>
public class InMemoryOrderRepository : IRepository<Order, string>
{
    private readonly Dictionary<string, Order> _orders = [];
    private readonly ILogger<InMemoryOrderRepository> _logger;

    public InMemoryOrderRepository(ILogger<InMemoryOrderRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving order {OrderId}", id);
        
        if (_orders.TryGetValue(id, out var order))
        {
            return await Task.FromResult(order).ConfigureAwait(false);
        }

        return await Task.FromResult<Order?>(null).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all orders");
        return await Task.FromResult(_orders.Values.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task SaveAsync(Order aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null)
            throw new ArgumentNullException(nameof(aggregate));

        _logger.LogInformation("Saving order {OrderId}", aggregate.Id);

        _orders[aggregate.Id] = aggregate;
        
        // Clear uncommitted events after saving
        aggregate.ClearUncommittedEvents();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting order {OrderId}", id);

        _orders.Remove(id);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// In-memory event store for Order domain events.
/// </summary>
public class InMemoryOrderEventStore : IEventStore
{
    private readonly List<DomainEventEnvelope> _events = [];
    private readonly ILogger<InMemoryOrderEventStore> _logger;

    public InMemoryOrderEventStore(ILogger<InMemoryOrderEventStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AppendAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));

        _logger.LogInformation("Appending event {EventType} for aggregate {AggregateId}", 
            envelope.DomainEvent.GetType().Name, envelope.DomainEvent.AggregateId);

        _events.Add(envelope);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetEventsByAggregateIdAsync(
        string aggregateId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving events for aggregate {AggregateId}", aggregateId);

        var events = _events.Where(e => e.DomainEvent.AggregateId == aggregateId).ToList();
        return await Task.FromResult(events.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all events");
        return await Task.FromResult(_events.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task<IEnumerable<DomainEventEnvelope>> GetEventsSinceAsync(
        long sequenceNumber, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving events since sequence {SequenceNumber}", sequenceNumber);
        
        // For in-memory, we'll use index-based approach
        var events = _events.Skip((int)sequenceNumber).ToList();
        return await Task.FromResult(events.AsEnumerable()).ConfigureAwait(false);
    }
}

/// <summary>
/// In-memory idempotency store to prevent duplicate order creation.
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _records = [];
    private readonly ILogger<InMemoryIdempotencyStore> _logger;

    public InMemoryIdempotencyStore(ILogger<InMemoryIdempotencyStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(
        string idempotencyKey, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking idempotency for key {IdempotencyKey}", idempotencyKey);

        if (_records.TryGetValue(idempotencyKey, out var record))
        {
            _logger.LogWarning("Idempotency key {IdempotencyKey} already processed", idempotencyKey);
            return new IdempotencyCheckResult
            {
                IsIdempotent = true,
                AggregateId = record.AggregateId,
                ProcessedAt = record.ProcessedAt
            };
        }

        return await Task.FromResult(new IdempotencyCheckResult { IsIdempotent = false }).ConfigureAwait(false);
    }

    public async Task MarkIdempotencyProcessedAsync(
        string idempotencyKey,
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Marking idempotency key {IdempotencyKey} as processed for aggregate {AggregateId}", 
            idempotencyKey, aggregateId);

        _records[idempotencyKey] = new IdempotencyRecord
        {
            IdempotencyKey = idempotencyKey,
            AggregateId = aggregateId,
            ProcessedAt = DateTime.UtcNow
        };

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
