using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Inventory.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Inventory.Application;

/// <summary>
/// Event handler for inventory events.
/// Handles domain events and triggers appropriate handlers (e.g., projection updates).
/// </summary>
public class InventoryEventHandler
{
    private readonly IInventoryProjectionWriter _projectionWriter;
    private readonly ILogger<InventoryEventHandler> _logger;

    public InventoryEventHandler(
        IInventoryProjectionWriter projectionWriter,
        ILogger<InventoryEventHandler> logger)
    {
        _projectionWriter = projectionWriter ?? throw new ArgumentNullException(nameof(projectionWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleStockItemCreatedAsync(
        StockItemCreatedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Handling StockItemCreated event for product {ProductId} with initial quantity {InitialQuantity} (CorrelationId: {CorrelationId})",
                @event.ProductId, @event.InitialQuantity, correlationId);

            await _projectionWriter.HandleStockItemCreatedAsync(@event, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StockItemCreated event for product {ProductId}", @event.ProductId);
            throw;
        }
    }

    public async Task HandleStockSetAsync(
        StockSetEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Handling StockSet event for product {ProductId}: {OldQuantity} -> {NewQuantity} (CorrelationId: {CorrelationId})",
                @event.ProductId, @event.OldQuantity, @event.NewQuantity, correlationId);

            await _projectionWriter.HandleStockSetAsync(@event, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StockSet event for product {ProductId}", @event.ProductId);
            throw;
        }
    }

    public async Task HandleStockDeductedForOrderAsync(
        StockDeductedForOrderEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Handling StockDeductedForOrder event for order {OrderId}, product {ProductId}, qty {QuantityDeducted} (CorrelationId: {CorrelationId})",
                @event.OrderId, @event.ProductId, @event.QuantityDeducted, correlationId);

            await _projectionWriter.HandleStockDeductedForOrderAsync(@event, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StockDeductedForOrder event for order {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleStockDeductionRejectedAsync(
        StockDeductionRejectedEvent @event,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning(
                "Handling StockDeductionRejected event for order {OrderId}, product {ProductId}: requested {RequestedQuantity}, available {AvailableQuantity} (CorrelationId: {CorrelationId})",
                @event.OrderId, @event.ProductId, @event.RequestedQuantity, @event.AvailableQuantity, correlationId);

            await _projectionWriter.HandleStockDeductionRejectedAsync(@event, correlationId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling StockDeductionRejected event for order {OrderId}", @event.OrderId);
            throw;
        }
    }
}
