using ECommerceMvp.Shared.Application;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.Inventory.Application;

#region Query Models

/// <summary>
/// Read model for stock availability view.
/// Used for quick stock checks without accessing event store.
/// </summary>
public class StockAvailabilityView
{
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public bool InStockFlag { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

/// <summary>
/// Read model for low stock view.
/// Tracks products with quantity below threshold.
/// </summary>
public class LowStockView
{
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsLow { get; set; }
    public DateTime AlertedAt { get; set; }
}

#endregion

#region Queries

/// <summary>
/// Query: Get stock availability for a product.
/// Query: GetStockAvailabilityQuery { productId }
/// </summary>
public class GetStockAvailabilityQuery : IQuery<StockAvailabilityView?>
{
    public string ProductId { get; set; } = string.Empty;
}

/// <summary>
/// Handler for GetStockAvailabilityQuery.
/// </summary>
public class GetStockAvailabilityQueryHandler : IQueryHandler<GetStockAvailabilityQuery, StockAvailabilityView?>
{
    private readonly IMongoCollection<StockAvailabilityView> _collection;
    private readonly ILogger<GetStockAvailabilityQueryHandler> _logger;

    public GetStockAvailabilityQueryHandler(
        IMongoClient mongoClient,
        string databaseName,
        ILogger<GetStockAvailabilityQueryHandler> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(databaseName);
        _collection = database.GetCollection<StockAvailabilityView>("StockAvailability");
    }

    public async Task<StockAvailabilityView?> HandleAsync(
        GetStockAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying stock availability for product {ProductId}", query.ProductId);

            var result = await _collection.Find(
                Builders<StockAvailabilityView>.Filter.Eq(s => s.ProductId, query.ProductId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying stock availability for product {ProductId}", query.ProductId);
            throw;
        }
    }
}

#endregion

#region Batch Queries

/// <summary>
/// Query: Get stock availability for multiple products.
/// Query: GetMultipleStockAvailabilityQuery { productIds }
/// </summary>
public class GetMultipleStockAvailabilityQuery : IQuery<List<StockAvailabilityView>>
{
    public List<string> ProductIds { get; set; } = new();
}

/// <summary>
/// Handler for GetMultipleStockAvailabilityQuery.
/// </summary>
public class GetMultipleStockAvailabilityQueryHandler : IQueryHandler<GetMultipleStockAvailabilityQuery, List<StockAvailabilityView>>
{
    private readonly IMongoCollection<StockAvailabilityView> _collection;
    private readonly ILogger<GetMultipleStockAvailabilityQueryHandler> _logger;

    public GetMultipleStockAvailabilityQueryHandler(
        IMongoClient mongoClient,
        string databaseName,
        ILogger<GetMultipleStockAvailabilityQueryHandler> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(databaseName);
        _collection = database.GetCollection<StockAvailabilityView>("StockAvailability");
    }

    public async Task<List<StockAvailabilityView>> HandleAsync(
        GetMultipleStockAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying stock availability for {ProductCount} products", query.ProductIds.Count);

            var results = await _collection.Find(
                Builders<StockAvailabilityView>.Filter.In(s => s.ProductId, query.ProductIds))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying multiple stock availability");
            throw;
        }
    }
}

#endregion

#region Projection Writers

/// <summary>
/// Interface for inventory projection writers.
/// </summary>
public interface IInventoryProjectionWriter
{
    Task HandleStockItemCreatedAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task HandleStockSetAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task HandleStockDeductedForOrderAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task HandleStockDeductionRejectedAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default);
}

#endregion
