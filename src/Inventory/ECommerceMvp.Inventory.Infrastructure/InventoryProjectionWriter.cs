using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Inventory.Domain;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.Inventory.Infrastructure;

/// <summary>
/// Inventory projection writer for read model updates.
/// Updates StockAvailabilityView and LowStockView based on domain events.
/// </summary>
public class InventoryProjectionWriter : IInventoryProjectionWriter
{
    private readonly IMongoCollection<StockAvailabilityView> _stockAvailabilityCollection;
    private readonly IMongoCollection<LowStockView> _lowStockCollection;
    private readonly ILogger<InventoryProjectionWriter> _logger;
    private const int LowStockThreshold = 10; // Configurable

    public InventoryProjectionWriter(
        IMongoClient mongoClient,
        string databaseName,
        ILogger<InventoryProjectionWriter> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase(databaseName);
        _stockAvailabilityCollection = database.GetCollection<StockAvailabilityView>("StockAvailability");
        _lowStockCollection = database.GetCollection<LowStockView>("LowStock");

        EnsureIndexes();
    }

    /// <summary>
    /// Projects StockItemCreatedEvent to StockAvailabilityView.
    /// </summary>
    public async Task HandleStockItemCreatedAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = (StockItemCreatedEvent)@event;
            var readModel = new StockAvailabilityView
            {
                ProductId = evt.ProductId,
                AvailableQuantity = evt.InitialQuantity,
                InStockFlag = evt.InitialQuantity > 0,
                LastUpdatedAt = evt.OccurredAt.DateTime
            };

            await _stockAvailabilityCollection.InsertOneAsync(readModel, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Projected StockItemCreated event for product {ProductId} with initial quantity {InitialQuantity} (CorrelationId: {CorrelationId})",
                evt.ProductId, evt.InitialQuantity, correlationId);

            // Create or update low stock view if initial quantity is low
            if (evt.InitialQuantity < LowStockThreshold)
            {
                var lowStockModel = new LowStockView
                {
                    ProductId = evt.ProductId,
                    AvailableQuantity = evt.InitialQuantity,
                    LowStockThreshold = LowStockThreshold,
                    IsLow = true,
                    AlertedAt = evt.OccurredAt.DateTime
                };

                await _lowStockCollection.InsertOneAsync(lowStockModel, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting StockItemCreated event (CorrelationId: {CorrelationId})", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Projects StockSetEvent to StockAvailabilityView.
    /// </summary>
    public async Task HandleStockSetAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = (StockSetEvent)@event;
            var update = Builders<StockAvailabilityView>.Update
                .Set(s => s.AvailableQuantity, evt.NewQuantity)
                .Set(s => s.InStockFlag, evt.NewQuantity > 0)
                .Set(s => s.LastUpdatedAt, evt.OccurredAt);

            var result = await _stockAvailabilityCollection.UpdateOneAsync(
                Builders<StockAvailabilityView>.Filter.Eq(s => s.ProductId, evt.ProductId),
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Projected StockSet event for product {ProductId}: {OldQuantity} -> {NewQuantity} (CorrelationId: {CorrelationId}, Matched: {Matched})",
                evt.ProductId, evt.OldQuantity, evt.NewQuantity, correlationId, result.MatchedCount);

            // Update low stock view
            if (evt.NewQuantity < LowStockThreshold)
            {
                var lowStockUpdate = Builders<LowStockView>.Update
                    .Set(s => s.AvailableQuantity, evt.NewQuantity)
                    .Set(s => s.IsLow, true)
                    .Set(s => s.AlertedAt, evt.OccurredAt);

                await _lowStockCollection.UpdateOneAsync(
                    Builders<LowStockView>.Filter.Eq(s => s.ProductId, evt.ProductId),
                    lowStockUpdate,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Remove from low stock if quantity is now sufficient
                await _lowStockCollection.DeleteOneAsync(
                    Builders<LowStockView>.Filter.Eq(s => s.ProductId, evt.ProductId),
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting StockSet event (CorrelationId: {CorrelationId})", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Projects StockDeductedForOrderEvent to StockAvailabilityView.
    /// </summary>
    public async Task HandleStockDeductedForOrderAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = (StockDeductedForOrderEvent)@event;
            var update = Builders<StockAvailabilityView>.Update
                .Set(s => s.AvailableQuantity, evt.NewQuantity)
                .Set(s => s.InStockFlag, evt.NewQuantity > 0)
                .Set(s => s.LastUpdatedAt, evt.OccurredAt);

            var result = await _stockAvailabilityCollection.UpdateOneAsync(
                Builders<StockAvailabilityView>.Filter.Eq(s => s.ProductId, evt.ProductId),
                update,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Projected StockDeductedForOrder event for order {OrderId}, product {ProductId}, qty {QuantityDeducted} (CorrelationId: {CorrelationId}, Matched: {Matched})",
                evt.OrderId, evt.ProductId, evt.QuantityDeducted, correlationId, result.MatchedCount);

            // Update low stock view
            if (evt.NewQuantity < LowStockThreshold)
            {
                var lowStockUpdate = Builders<LowStockView>.Update
                    .Set(s => s.AvailableQuantity, evt.NewQuantity)
                    .Set(s => s.IsLow, true)
                    .Set(s => s.AlertedAt, evt.OccurredAt);

                await _lowStockCollection.UpdateOneAsync(
                    Builders<LowStockView>.Filter.Eq(s => s.ProductId, evt.ProductId),
                    lowStockUpdate,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting StockDeductedForOrder event (CorrelationId: {CorrelationId})", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Logs StockDeductionRejectedEvent (no read model update needed, event for audit trail).
    /// </summary>
    public async Task HandleStockDeductionRejectedAsync(
        dynamic @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evt = (StockDeductionRejectedEvent)@event;

            _logger.LogWarning(
                "Projected StockDeductionRejected event for order {OrderId}, product {ProductId}: requested {RequestedQuantity}, available {AvailableQuantity} (CorrelationId: {CorrelationId})",
                evt.OrderId, evt.ProductId, evt.RequestedQuantity, evt.AvailableQuantity, correlationId);

            // This event is primarily for audit trail; no read model update needed
            // Could optionally store in a rejection log collection if needed
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error projecting StockDeductionRejected event (CorrelationId: {CorrelationId})", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Ensure MongoDB indexes for optimal query performance.
    /// </summary>
    private void EnsureIndexes()
    {
        try
        {
            // Index on ProductId for StockAvailabilityView
            var stockIndexModel = new CreateIndexModel<StockAvailabilityView>(
                Builders<StockAvailabilityView>.IndexKeys.Ascending(s => s.ProductId),
                new CreateIndexOptions { Unique = true });
            _stockAvailabilityCollection.Indexes.CreateOne(stockIndexModel);

            // Index on ProductId for LowStockView
            var lowStockIndexModel = new CreateIndexModel<LowStockView>(
                Builders<LowStockView>.IndexKeys.Ascending(s => s.ProductId),
                new CreateIndexOptions { Unique = true });
            _lowStockCollection.Indexes.CreateOne(lowStockIndexModel);

            // Index on IsLow for efficient low stock queries
            var isLowIndexModel = new CreateIndexModel<LowStockView>(
                Builders<LowStockView>.IndexKeys.Ascending(s => s.IsLow));
            _lowStockCollection.Indexes.CreateOne(isLowIndexModel);

            _logger.LogInformation("Inventory indexes ensured");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error ensuring indexes (may already exist)");
        }
    }
}
