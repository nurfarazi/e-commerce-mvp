using ECommerceMvp.Shared.Application;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.Shared.Infrastructure;

/// <summary>
/// Idempotency store implementation using MongoDB.
/// </summary>
public class MongoIdempotencyStore : IIdempotencyStore
{
    private readonly IMongoCollection<ProcessedCommandDocument> _commandCollection;
    private readonly IMongoCollection<ProcessedEventDocument> _eventCollection;
    private readonly IMongoCollection<IdempotencyKeyDocument> _idempotencyCollection;
    private readonly ILogger<MongoIdempotencyStore> _logger;

    public MongoIdempotencyStore(IMongoClient mongoClient, MongoDbOptions options, ILogger<MongoIdempotencyStore> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(options.DatabaseName);
        _commandCollection = database.GetCollection<ProcessedCommandDocument>("ProcessedCommands");
        _eventCollection = database.GetCollection<ProcessedEventDocument>("ProcessedEvents");
        _idempotencyCollection = database.GetCollection<IdempotencyKeyDocument>("IdempotencyKeys");

        EnsureIndexes();
    }

    public async Task<bool> IsCommandProcessedAsync(string commandId, CancellationToken cancellationToken = default)
    {
        var result = await _commandCollection.FindAsync(
            Builders<ProcessedCommandDocument>.Filter.Eq(c => c.CommandId, commandId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await result.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetCommandResultAsync<T>(string commandId, CancellationToken cancellationToken = default)
    {
        var result = await _commandCollection.FindAsync(
            Builders<ProcessedCommandDocument>.Filter.Eq(c => c.CommandId, commandId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var doc = await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (doc == null)
            return default;

        return System.Text.Json.JsonSerializer.Deserialize<T>(doc.Result);
    }

    public async Task MarkCommandAsProcessedAsync(string commandId, object result, CancellationToken cancellationToken = default)
    {
        var doc = new ProcessedCommandDocument
        {
            CommandId = commandId,
            Result = System.Text.Json.JsonSerializer.Serialize(result),
            ProcessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await _commandCollection.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Marked command {CommandId} as processed", commandId);
    }

    public async Task<bool> IsEventProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var result = await _eventCollection.FindAsync(
            Builders<ProcessedEventDocument>.Filter.And(
                Builders<ProcessedEventDocument>.Filter.Eq(e => e.EventId, eventId),
                Builders<ProcessedEventDocument>.Filter.Eq(e => e.HandlerName, handlerName)),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await result.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkEventAsProcessedAsync(string eventId, string handlerName, CancellationToken cancellationToken = default)
    {
        var doc = new ProcessedEventDocument
        {
            EventId = eventId,
            HandlerName = handlerName,
            ProcessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await _eventCollection.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Marked event {EventId} as processed by {HandlerName}", eventId, handlerName);
    }

    public async Task<IdempotencyCheckResult> CheckIdempotencyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await _idempotencyCollection.FindAsync(
            Builders<IdempotencyKeyDocument>.Filter.Eq(i => i.IdempotencyKey, idempotencyKey),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var doc = await result.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (doc == null)
        {
            return new IdempotencyCheckResult { IsIdempotent = false };
        }

        return new IdempotencyCheckResult
        {
            IsIdempotent = true,
            AggregateId = doc.AggregateId,
            ProcessedAt = doc.ProcessedAt.UtcDateTime
        };
    }

    public async Task MarkIdempotencyProcessedAsync(
        string idempotencyKey,
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        var doc = new IdempotencyKeyDocument
        {
            IdempotencyKey = idempotencyKey,
            AggregateId = aggregateId,
            ProcessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await _idempotencyCollection.ReplaceOneAsync(
            Builders<IdempotencyKeyDocument>.Filter.Eq(i => i.IdempotencyKey, idempotencyKey),
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    private void EnsureIndexes()
    {
        // Command index
        var commandIndex = Builders<ProcessedCommandDocument>.IndexKeys.Ascending(c => c.CommandId);
        _commandCollection.Indexes.CreateOne(new CreateIndexModel<ProcessedCommandDocument>(commandIndex));

        var commandExpiryIndex = Builders<ProcessedCommandDocument>.IndexKeys.Ascending(c => c.ExpiresAt);
        _commandCollection.Indexes.CreateOne(
            new CreateIndexModel<ProcessedCommandDocument>(
                commandExpiryIndex,
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }));

        // Event indexes
        var eventIndex = Builders<ProcessedEventDocument>.IndexKeys
            .Ascending(e => e.EventId)
            .Ascending(e => e.HandlerName);
        _eventCollection.Indexes.CreateOne(new CreateIndexModel<ProcessedEventDocument>(eventIndex));

        var eventExpiryIndex = Builders<ProcessedEventDocument>.IndexKeys.Ascending(e => e.ExpiresAt);
        _eventCollection.Indexes.CreateOne(
            new CreateIndexModel<ProcessedEventDocument>(
                eventExpiryIndex,
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(7) }));

        // Idempotency key indexes
        var idempotencyKeyIndex = Builders<IdempotencyKeyDocument>.IndexKeys.Ascending(i => i.IdempotencyKey);
        _idempotencyCollection.Indexes.CreateOne(
            new CreateIndexModel<IdempotencyKeyDocument>(
                idempotencyKeyIndex,
                new CreateIndexOptions { Unique = true }));

        var idempotencyExpiryIndex = Builders<IdempotencyKeyDocument>.IndexKeys.Ascending(i => i.ExpiresAt);
        _idempotencyCollection.Indexes.CreateOne(
            new CreateIndexModel<IdempotencyKeyDocument>(
                idempotencyExpiryIndex,
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }));
    }

    private class ProcessedCommandDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CommandId { get; set; } = null!;
        public string Result { get; set; } = null!;
        public DateTimeOffset ProcessedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private class ProcessedEventDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventId { get; set; } = null!;
        public string HandlerName { get; set; } = null!;
        public DateTimeOffset ProcessedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private class IdempotencyKeyDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string IdempotencyKey { get; set; } = null!;
        public string AggregateId { get; set; } = null!;
        public DateTimeOffset ProcessedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
