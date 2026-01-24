using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Shared.Infrastructure;
using ECommerceMvp.Checkout.Domain;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using CheckoutSagaAggregate = ECommerceMvp.Checkout.Domain.CheckoutSaga;

namespace ECommerceMvp.Checkout.Infrastructure;

/// <summary>
/// Repository for CheckoutSaga aggregate using MongoDB event sourcing.
/// </summary>
public class CheckoutRepository : IRepository<CheckoutSagaAggregate, string>
{
    private readonly IMongoCollection<BsonDocument> _eventsCollection;
    private readonly IMongoCollection<BsonDocument> _snapshotsCollection;
    private readonly ILogger<CheckoutRepository> _logger;
    private readonly int _snapshotFrequency = 5; // Create snapshot every 5 events

    public CheckoutRepository(
        IMongoDatabase database,
        ILogger<CheckoutRepository> logger)
    {
        _eventsCollection = database.GetCollection<BsonDocument>("checkout_events");
        _snapshotsCollection = database.GetCollection<BsonDocument>("checkout_snapshots");
        _logger = logger;

        // Ensure indexes
        EnsureIndexes();
    }

    public async Task<CheckoutSagaAggregate?> GetByIdAsync(string id)
    {
        try
        {
            // Try to load from snapshot first
            var snapshot = await LoadSnapshotAsync(id);
            if (snapshot != null)
            {
                _logger.LogDebug("Loaded CheckoutSaga {Id} from snapshot at version {Version}",
                    id, snapshot.Version);
                return snapshot;
            }

            // Load from events
            var events = await LoadEventsAsync(id);
            if (!events.Any())
            {
                return null;
            }

            var saga = new CheckoutSagaAggregate(id);
            saga.LoadFromHistory(events);

            _logger.LogDebug("Loaded CheckoutSaga {Id} from {EventCount} events", id, events.Count);
            return saga;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading CheckoutSaga {Id}", id);
            throw;
        }
    }

    public async Task SaveAsync(CheckoutSagaAggregate aggregate)
    {
        try
        {
            if (!aggregate.UncommittedEvents.Any())
            {
                return;
            }

            // Save events
            foreach (var @event in aggregate.UncommittedEvents)
            {
                var eventDoc = EventToDocument(@event, aggregate.Id);
                await _eventsCollection.InsertOneAsync(eventDoc);
            }

            _logger.LogDebug("Saved {EventCount} events for CheckoutSaga {Id}",
                aggregate.UncommittedEvents.Count, aggregate.Id);

            // Create snapshot if needed
            var allEvents = await LoadEventsAsync(aggregate.Id);
            if (allEvents.Count % _snapshotFrequency == 0)
            {
                await SaveSnapshotAsync(aggregate);
            }

            aggregate.ClearUncommittedEvents();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving CheckoutSaga {Id}", aggregate.Id);
            throw;
        }
    }

    private async Task<List<IDomainEvent>> LoadEventsAsync(string aggregateId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("aggregate_id", aggregateId);
        var documents = await _eventsCollection
            .Find(filter)
            .SortBy(d => d["version"])
            .ToListAsync();

        return documents.Select(DocumentToEvent).ToList();
    }

    private async Task<CheckoutSagaAggregate?> LoadSnapshotAsync(string aggregateId)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("aggregate_id", aggregateId);
        var snapshot = await _snapshotsCollection
            .Find(filter)
            .SortByDescending(d => d["version"])
            .FirstOrDefaultAsync();

        if (snapshot == null)
        {
            return null;
        }

        var saga = DocumentToSnapshot(snapshot);

        // Load events after snapshot version
        var version = snapshot["version"].AsInt32;
        var filter2 = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("aggregate_id", aggregateId),
            Builders<BsonDocument>.Filter.Gt("version", version));
        var events = await _eventsCollection
            .Find(filter2)
            .SortBy(d => d["version"])
            .ToListAsync();

        foreach (var evt in events)
        {
            saga.ApplyEvent(DocumentToEvent(evt));
        }

        return saga;
    }

    private async Task SaveSnapshotAsync(CheckoutSagaAggregate aggregate)
    {
        var snapshotDoc = SnapshotToDocument(aggregate);
        var filter = Builders<BsonDocument>.Filter.Eq("aggregate_id", aggregate.Id);
        await _snapshotsCollection.ReplaceOneAsync(filter, snapshotDoc, new ReplaceOptions { IsUpsert = true });

        _logger.LogDebug("Saved snapshot for CheckoutSaga {Id} at version {Version}",
            aggregate.Id, aggregate.Version);
    }

    private BsonDocument EventToDocument(IDomainEvent @event, string aggregateId)
    {
        return new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "event_id", @event.EventId },
            { "aggregate_id", aggregateId },
            { "event_type", @event.EventType },
            { "event_version", @event.EventVersion },
            { "version", 0 }, // Will be set by MongoDB
            { "occurred_at", @event.OccurredAt.DateTime },
            { "payload", BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(@event)) }
        };
    }

    private IDomainEvent DocumentToEvent(BsonDocument doc)
    {
        var eventType = doc["event_type"].AsString;
        var payload = doc["payload"].AsString;

        return eventType switch
        {
            "ECommerceMvp.Checkout.Domain.CheckoutSagaInitiatedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<CheckoutSagaInitiatedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.CartSnapshotReceivedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<CartSnapshotReceivedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.ProductSnapshotsReceivedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<ProductSnapshotsReceivedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.StockValidationCompletedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<StockValidationCompletedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.StockDeductedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<StockDeductedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.OrderCreatedInSagaEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<OrderCreatedInSagaEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.CartClearedInSagaEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<CartClearedInSagaEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.OrderFinalizedInSagaEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<OrderFinalizedInSagaEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.CheckoutSagaFailedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<CheckoutSagaFailedEvent>(payload) ??
                throw new InvalidOperationException(),
            "ECommerceMvp.Checkout.Domain.CheckoutSagaCompletedEvent" =>
                System.Text.Json.JsonSerializer.Deserialize<CheckoutSagaCompletedEvent>(payload) ??
                throw new InvalidOperationException(),
            _ => throw new InvalidOperationException($"Unknown event type: {eventType}")
        };
    }

    private BsonDocument SnapshotToDocument(CheckoutSagaAggregate aggregate)
    {
        return new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "aggregate_id", aggregate.Id },
            { "version", aggregate.Version },
            { "status", aggregate.Status.ToString() },
            { "checkout_id", aggregate.CheckoutId },
            { "order_id", aggregate.OrderId },
            { "guest_token", aggregate.GuestToken },
            { "cart_id", aggregate.CartId },
            { "failure_reason", aggregate.FailureReason ?? "" },
            { "initiated_at", aggregate.InitiatedAt },
            { "completed_at", aggregate.CompletedAt?.DateTime },
            { "snapshot_at", DateTime.UtcNow }
        };
    }

    private CheckoutSagaAggregate DocumentToSnapshot(BsonDocument doc)
    {
        var saga = new CheckoutSagaAggregate(doc["aggregate_id"].AsString)
        {
            CheckoutId = doc["checkout_id"].AsString,
            OrderId = doc["order_id"].AsString,
            GuestToken = doc["guest_token"].AsString,
            CartId = doc["cart_id"].AsString,
            FailureReason = doc.Contains("failure_reason") ? doc["failure_reason"].AsString : null
        };

        saga.Version = doc["version"].AsInt32;

        // Reconstruct status (this is simplified - in a real scenario you'd store serialized state)
        var statusStr = doc["status"].AsString;
        if (Enum.TryParse<CheckoutSagaStatus>(statusStr, out var status))
        {
            saga.ApplyEvent(new CheckoutSagaInitiatedEvent { AggregateId = saga.Id });
        }

        return saga;
    }

    private void EnsureIndexes()
    {
        var aggregateIdIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("aggregate_id"));
        _eventsCollection.Indexes.CreateOne(aggregateIdIndex);

        var snapshotIndexes = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("aggregate_id"));
        _snapshotsCollection.Indexes.CreateOne(snapshotIndexes);
    }
}
