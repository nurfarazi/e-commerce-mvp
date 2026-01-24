using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Domain;
using OrderAggregate = ECommerceMvp.Order.Domain.Order;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Order.Infrastructure;

/// <summary>
/// In-memory repository implementation for Order aggregate.
/// In production, this would be replaced with a database-backed repository.
/// </summary>
public class InMemoryOrderRepository : IRepository<OrderAggregate, string>
{
    private readonly Dictionary<string, OrderAggregate> _orders = [];
    private readonly ILogger<InMemoryOrderRepository> _logger;

    public InMemoryOrderRepository(ILogger<InMemoryOrderRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving order {OrderId}", id);
        
        if (_orders.TryGetValue(id, out var order))
        {
            return await Task.FromResult(order).ConfigureAwait(false);
        }

        return await Task.FromResult<OrderAggregate?>(null).ConfigureAwait(false);
    }

    public async Task<IEnumerable<OrderAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all orders");
        return await Task.FromResult(_orders.Values.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task SaveAsync(OrderAggregate aggregate, CancellationToken cancellationToken = default)
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
    private readonly List<InMemoryEventRecord> _events = [];
    private readonly Dictionary<string, int> _streamVersions = [];
    private readonly ILogger<InMemoryOrderEventStore> _logger;

    public InMemoryOrderEventStore(ILogger<InMemoryOrderEventStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AppendAsync(
        string streamId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        string correlationId,
        string? causationId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var eventsList = events.ToList();
        if (eventsList.Count == 0)
            return;

        _streamVersions.TryGetValue(streamId, out var currentVersion);
        if (currentVersion != expectedVersion)
        {
            _logger.LogWarning(
                "Concurrency conflict for stream {StreamId}: expected version {ExpectedVersion}, actual {ActualVersion}",
                streamId, expectedVersion, currentVersion);
            throw new ConcurrencyException(
                $"Concurrency conflict: expected version {expectedVersion}, but current version is {currentVersion}");
        }

        var nextVersion = currentVersion;
        foreach (var evt in eventsList)
        {
            nextVersion++;
            _logger.LogInformation("Appending event {EventType} for aggregate {AggregateId}",
                evt.GetType().Name, evt.AggregateId);

            _events.Add(new InMemoryEventRecord
            {
                StreamId = streamId,
                Version = nextVersion,
                DomainEvent = evt,
                CorrelationId = correlationId,
                CausationId = causationId,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _streamVersions[streamId] = nextVersion;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving events for stream {StreamId}", streamId);

        var events = _events
            .Where(e => e.StreamId == streamId)
            .OrderBy(e => e.Version)
            .Select(e => e.DomainEvent)
            .ToList();

        return await Task.FromResult(events.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamFromVersionAsync(
        string streamId,
        int fromVersion,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving events for stream {StreamId} from version {FromVersion}", streamId, fromVersion);

        var events = _events
            .Where(e => e.StreamId == streamId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .Select(e => e.DomainEvent)
            .ToList();

        return await Task.FromResult(events.AsEnumerable()).ConfigureAwait(false);
    }
}

public class InMemoryEventRecord
{
    public string StreamId { get; set; } = string.Empty;
    public int Version { get; set; }
    public IDomainEvent DomainEvent { get; set; } = null!;
    public string CorrelationId { get; set; } = string.Empty;
    public string? CausationId { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// In-memory idempotency store to prevent duplicate order creation.
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _records = [];
    private readonly Dictionary<string, object?> _commandResults = [];
    private readonly HashSet<string> _processedEvents = [];
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

    public async Task<bool> IsCommandProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_commandResults.ContainsKey(commandId)).ConfigureAwait(false);
    }

    public async Task<T?> GetCommandResultAsync<T>(string commandId, CancellationToken cancellationToken = default)
    {
        if (_commandResults.TryGetValue(commandId, out var result))
        {
            if (result is T typedResult)
                return await Task.FromResult(typedResult).ConfigureAwait(false);

            if (result == null)
                return await Task.FromResult<T?>(default).ConfigureAwait(false);
        }

        return await Task.FromResult<T?>(default).ConfigureAwait(false);
    }

    public async Task MarkCommandAsProcessedAsync(string commandId, object result, CancellationToken cancellationToken = default)
    {
        _commandResults[commandId] = result;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<bool> IsEventProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var key = $"{eventId}:{handlerName}";
        return await Task.FromResult(_processedEvents.Contains(key)).ConfigureAwait(false);
    }

    public async Task MarkEventAsProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var key = $"{eventId}:{handlerName}";
        _processedEvents.Add(key);
        await Task.CompletedTask.ConfigureAwait(false);
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
