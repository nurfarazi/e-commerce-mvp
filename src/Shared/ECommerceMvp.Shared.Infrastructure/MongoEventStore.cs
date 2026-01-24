using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.Shared.Infrastructure;

/// <summary>
/// Configuration for MongoDB connection.
/// </summary>
public class MongoDbOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "ecommerce";
    public int MaxPoolSize { get; set; } = 50;
    public int MinPoolSize { get; set; } = 10;
}

/// <summary>
/// Event store implementation using MongoDB.
/// </summary>
public class MongoEventStore : IEventStore
{
    private readonly IMongoCollection<EventDocument> _eventsCollection;
    private readonly IMongoCollection<StreamMetadataDocument> _metadataCollection;
    private readonly ILogger<MongoEventStore> _logger;

    public MongoEventStore(IMongoClient mongoClient, MongoDbOptions options, ILogger<MongoEventStore> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(options.DatabaseName);
        _eventsCollection = database.GetCollection<EventDocument>("Events");
        _metadataCollection = database.GetCollection<StreamMetadataDocument>("StreamMetadata");

        EnsureIndexes();
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
        var eventsList = events.ToList();
        if (eventsList.Count == 0)
            return;

        // Get current stream metadata
        var metadata = await _metadataCollection.FindAsync(
            Builders<StreamMetadataDocument>.Filter.Eq(m => m.StreamId, streamId),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var currentMetadata = await metadata.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var currentVersion = currentMetadata?.Version ?? 0;

        // Optimistic concurrency check
        if (currentVersion != expectedVersion)
        {
            _logger.LogWarning(
                "Concurrency conflict for stream {StreamId}: expected version {ExpectedVersion}, actual {ActualVersion}",
                streamId, expectedVersion, currentVersion);
            throw new ConcurrencyException(
                $"Concurrency conflict: expected version {expectedVersion}, but current version is {currentVersion}");
        }

        // Build event documents
        var eventDocuments = new List<EventDocument>();
        var newVersion = expectedVersion;

        foreach (var evt in eventsList)
        {
            newVersion++;
            eventDocuments.Add(new EventDocument
            {
                StreamId = streamId,
                Version = newVersion,
                EventId = evt.EventId,
                EventType = evt.EventType,
                EventVersion = evt.EventVersion,
                Payload = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType()),
                CorrelationId = correlationId,
                CausationId = causationId,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        using (var session = _eventsCollection.Database.Client.StartSession())
        {
            try
            {
                // Try to start a transaction (only works with replica sets)
                try
                {
                    session.StartTransaction();
                }
                catch (NotSupportedException)
                {
                    _logger.LogWarning("Transactions not supported (standalone MongoDB). Using non-transactional writes.");
                }

                // Insert events
                await _eventsCollection.InsertManyAsync(session, eventDocuments, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Update or insert stream metadata
                if (currentMetadata == null)
                {
                    await _metadataCollection.InsertOneAsync(session,
                        new StreamMetadataDocument
                        {
                            StreamId = streamId,
                            Version = newVersion,
                            TenantId = tenantId,
                            CreatedAt = DateTimeOffset.UtcNow,
                            LastModifiedAt = DateTimeOffset.UtcNow
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _metadataCollection.UpdateOneAsync(session,
                        Builders<StreamMetadataDocument>.Filter.Eq(m => m.StreamId, streamId),
                        Builders<StreamMetadataDocument>.Update
                            .Set(m => m.Version, newVersion)
                            .Set(m => m.LastModifiedAt, DateTimeOffset.UtcNow),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (session.IsInTransaction)
                {
                    await session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.LogDebug("Appended {EventCount} events to stream {StreamId}, new version {NewVersion}",
                    eventDocuments.Count, streamId, newVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending events to stream {StreamId}", streamId);
                if (session.IsInTransaction)
                {
                    try
                    {
                        await session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch { }
                }
                throw;
            }
        }
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<EventDocument>.Filter.Eq(e => e.StreamId, streamId);
        var options = new FindOptions<EventDocument> { Sort = Builders<EventDocument>.Sort.Ascending(e => e.Version) };
        var cursor = await _eventsCollection
            .FindAsync(filter, options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var documents = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Loaded {EventCount} events from stream {StreamId}", documents.Count, streamId);

        return documents
            .Select(doc => System.Text.Json.JsonSerializer.Deserialize<IDomainEvent>(doc.Payload)!)
            .ToList();
    }

    public async Task<IEnumerable<IDomainEvent>> LoadStreamFromVersionAsync(
        string streamId,
        int fromVersion,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(e => e.StreamId, streamId),
            Builders<EventDocument>.Filter.Gt(e => e.Version, fromVersion));

        var options = new FindOptions<EventDocument> { Sort = Builders<EventDocument>.Sort.Ascending(e => e.Version) };
        var cursor = await _eventsCollection
            .FindAsync(filter, options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var documents = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents
            .Select(doc => System.Text.Json.JsonSerializer.Deserialize<IDomainEvent>(doc.Payload)!)
            .ToList();
    }

    private void EnsureIndexes()
    {
        // Create indexes for event store
        var streamIdIndex = Builders<EventDocument>.IndexKeys.Ascending(e => e.StreamId);
        _eventsCollection.Indexes.CreateOne(new CreateIndexModel<EventDocument>(streamIdIndex));

        var versionIndex = Builders<EventDocument>.IndexKeys
            .Ascending(e => e.StreamId)
            .Ascending(e => e.Version);
        _eventsCollection.Indexes.CreateOne(new CreateIndexModel<EventDocument>(versionIndex));

        var eventIdIndex = Builders<EventDocument>.IndexKeys.Ascending(e => e.EventId);
        _eventsCollection.Indexes.CreateOne(new CreateIndexModel<EventDocument>(eventIdIndex));

        var correlationIdIndex = Builders<EventDocument>.IndexKeys.Ascending(e => e.CorrelationId);
        _eventsCollection.Indexes.CreateOne(new CreateIndexModel<EventDocument>(correlationIdIndex));
    }

    private class EventDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string StreamId { get; set; } = null!;
        public int Version { get; set; }
        public string EventId { get; set; } = null!;
        public string EventType { get; set; } = null!;
        public int EventVersion { get; set; }
        public string Payload { get; set; } = null!;
        public string CorrelationId { get; set; } = null!;
        public string? CausationId { get; set; }
        public string? TenantId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private class StreamMetadataDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string StreamId { get; set; } = null!;
        public int Version { get; set; }
        public string? TenantId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastModifiedAt { get; set; }
    }
}
