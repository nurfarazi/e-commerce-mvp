using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Domain;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.ProductCatalog.Infrastructure;

/// <summary>
/// Product projection writer for read model updates.
/// Updates both ProductListView and ProductDetailView projections based on domain events.
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

    /// <summary>
    /// Projects ProductCreatedEvent to ProductListView and ProductDetailView.
    /// </summary>
    public async Task HandleProductCreatedAsync(
        ProductCreatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var readModel = new ProductReadModel
            {
                ProductId = @event.ProductId,
                Sku = @event.Sku,
                Name = @event.Name,
                Description = @event.Description,
                Price = @event.Price,
                Currency = @event.Currency,
                IsActive = false,
                CreatedAt = @event.OccurredAt,
                LastModifiedAt = @event.OccurredAt
            };

            await _collection.InsertOneAsync(readModel, cancellationToken: cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Projected ProductCreated event for {ProductId} (CorrelationId: {CorrelationId})",
                @event.ProductId, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting ProductCreated event for {ProductId}", @event.ProductId);
            throw;
        }
    }

    /// <summary>
    /// Projects ProductDetailsUpdatedEvent to ProductDetailView.
    /// </summary>
    public async Task HandleProductDetailsUpdatedAsync(
        ProductDetailsUpdatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ProductReadModel>.Update
                .Set(p => p.Name, @event.Name)
                .Set(p => p.Description, @event.Description)
                .Set(p => p.LastModifiedAt, @event.OccurredAt);

            var result = await _collection.UpdateOneAsync(
                Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
                update,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Projected ProductDetailsUpdated event for {ProductId} (CorrelationId: {CorrelationId}, Matched: {Matched})",
                @event.ProductId, correlationId, result.MatchedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting ProductDetailsUpdated event for {ProductId}", @event.ProductId);
            throw;
        }
    }

    /// <summary>
    /// Projects ProductPriceChangedEvent to ProductListView and ProductDetailView.
    /// </summary>
    public async Task HandleProductPriceChangedAsync(
        ProductPriceChangedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ProductReadModel>.Update
                .Set(p => p.Price, @event.NewPrice)
                .Set(p => p.Currency, @event.NewCurrency)
                .Set(p => p.LastModifiedAt, @event.OccurredAt);

            var result = await _collection.UpdateOneAsync(
                Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
                update,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Projected ProductPriceChanged event for {ProductId} ({OldPrice} -> {NewPrice}) (CorrelationId: {CorrelationId})",
                @event.ProductId, @event.OldPrice, @event.NewPrice, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting ProductPriceChanged event for {ProductId}", @event.ProductId);
            throw;
        }
    }

    /// <summary>
    /// Projects ProductActivatedEvent to update IsActive flag.
    /// </summary>
    public async Task HandleProductActivatedAsync(
        ProductActivatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ProductReadModel>.Update
                .Set(p => p.IsActive, true)
                .Set(p => p.LastModifiedAt, @event.OccurredAt);

            var result = await _collection.UpdateOneAsync(
                Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
                update,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Projected ProductActivated event for {ProductId} (CorrelationId: {CorrelationId})",
                @event.ProductId, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting ProductActivated event for {ProductId}", @event.ProductId);
            throw;
        }
    }

    /// <summary>
    /// Projects ProductDeactivatedEvent to update IsActive flag.
    /// </summary>
    public async Task HandleProductDeactivatedAsync(
        ProductDeactivatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var update = Builders<ProductReadModel>.Update
                .Set(p => p.IsActive, false)
                .Set(p => p.LastModifiedAt, @event.OccurredAt);

            var result = await _collection.UpdateOneAsync(
                Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, @event.ProductId),
                update,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Projected ProductDeactivated event for {ProductId} (CorrelationId: {CorrelationId})",
                @event.ProductId, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting ProductDeactivated event for {ProductId}", @event.ProductId);
            throw;
        }
    }

    private void EnsureIndexes()
    {
        try
        {
            // Index on ProductId (primary lookup)
            var idIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.ProductId);
            _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(idIndex));

            // Index on IsActive (for ListActiveProductsQuery)
            var activeIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.IsActive);
            _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(activeIndex));

            // Index on Sku (unique constraint - SKU is immutable and unique)
            var skuIndex = Builders<ProductReadModel>.IndexKeys.Ascending(p => p.Sku);
            var skuIndexModel = new CreateIndexModel<ProductReadModel>(skuIndex, new CreateIndexOptions { Unique = true });
            _collection.Indexes.CreateOne(skuIndexModel);

            // Index on Name (for SearchProductsByNameQuery)
            var nameIndex = Builders<ProductReadModel>.IndexKeys.Text(p => p.Name);
            _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(nameIndex));

            // Compound index for pagination queries
            var paginationIndex = Builders<ProductReadModel>.IndexKeys
                .Ascending(p => p.IsActive)
                .Ascending(p => p.CreatedAt);
            _collection.Indexes.CreateOne(new CreateIndexModel<ProductReadModel>(paginationIndex));

            _logger.LogDebug("Product indexes ensured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error ensuring indexes");
        }
    }
}

/// <summary>
/// Product read model document for MongoDB.
/// Represents both ProductListView and ProductDetailView projections.
/// </summary>
public class ProductReadModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}
