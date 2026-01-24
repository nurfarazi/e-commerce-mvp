using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Domain;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.ProductCatalog.Infrastructure;

/// <summary>
/// Product projection writer for read model updates.
/// </summary>
public class ProductProjectionWriter : IProductProjectionWriter
{
    private readonly IMongoCollection<ProductReadModel> _collection;
    private readonly ILogger<ProductProjectionWriter> _logger;

    public ProductProjectionWriter(IMongoClient mongoClient, string databaseName, ILogger<ProductProjectionWriter> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(databaseName);
        _collection = database.GetCollection<ProductReadModel>("Products");

        EnsureIndexes();
    }

    public async Task HandleProductCreatedAsync(
        ProductCreatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var readModel = new ProductReadModel
        {
            ProductId = @event.ProductId,
            Name = @event.Name,
            Description = @event.Description,
            Sku = @event.Sku,
            Price = @event.Price,
            Currency = @event.Currency,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        await _collection.InsertOneAsync(readModel, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Created product read model for {ProductId}", @event.ProductId);
    }

    public async Task HandleProductUpdatedAsync(
        ProductUpdatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<ProductReadModel>.Update
            .Set(p => p.Name, @event.Name)
            .Set(p => p.Description, @event.Description)
            .Set(p => p.Price, @event.Price)
            .Set(p => p.Currency, @event.Currency)
            .Set(p => p.LastModifiedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(
            Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Updated product read model for {ProductId}", @event.ProductId);
    }

    public async Task HandleProductActivatedAsync(
        ProductActivatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<ProductReadModel>.Update
            .Set(p => p.IsActive, true)
            .Set(p => p.LastModifiedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(
            Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Activated product read model for {ProductId}", @event.ProductId);
    }

    public async Task HandleProductDeactivatedAsync(
        ProductDeactivatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<ProductReadModel>.Update
            .Set(p => p.IsActive, false)
            .Set(p => p.LastModifiedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(
            Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Deactivated product read model for {ProductId}", @event.ProductId);
    }

    private void EnsureIndexes()
    {
        var idIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.ProductId);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(idIndex));

        var activeIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.IsActive);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(activeIndex));

        var skuIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.Sku);
        _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(skuIndex));
    }
}

/// <summary>
/// Product read model document for MongoDB.
/// </summary>
public class ProductReadModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}
