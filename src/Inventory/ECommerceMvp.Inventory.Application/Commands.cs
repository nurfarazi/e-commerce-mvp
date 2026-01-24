using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Inventory.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Inventory.Application;

#region SetStockCommand

/// <summary>
/// Command: Set stock quantity for a product (admin operation).
/// Command: SetStockCommand { productId, newQty, reason? }
/// </summary>
public class SetStockCommand : ICommand<SetStockResponse>
{
    public string ProductId { get; set; } = string.Empty;
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}

public class SetStockResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for SetStockCommand.
/// </summary>
public class SetStockCommandHandler : ICommandHandler<SetStockCommand, SetStockResponse>
{
    private readonly IRepository<InventoryItem, string> _inventoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<SetStockCommandHandler> _logger;

    public SetStockCommandHandler(
        IRepository<InventoryItem, string> inventoryRepository,
        IEventPublisher eventPublisher,
        ILogger<SetStockCommandHandler> logger)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetStockResponse> HandleAsync(
        SetStockCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Setting stock for product {ProductId} to {NewQuantity}", 
                command.ProductId, command.NewQuantity);

            // Validate command
            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new SetStockResponse { Success = false, Error = "ProductId is required" };

            if (command.NewQuantity < 0)
                return new SetStockResponse { Success = false, Error = "Quantity cannot be negative" };

            // Load or create inventory item
            var item = await _inventoryRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            
            if (item == null)
            {
                // Create new inventory item with initial quantity
                item = InventoryItem.Create(command.ProductId, command.NewQuantity);
            }
            else
            {
                // Update existing item
                item.SetStock(command.NewQuantity, command.Reason, command.ChangedBy);
            }

            // Capture events BEFORE saving
            var envelopes = item.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(
                    evt,
                    Guid.NewGuid().ToString(), // CorrelationId
                    null,
                    null,
                    null))
                .ToList();

            // Save aggregate
            await _inventoryRepository.SaveAsync(item, cancellationToken).ConfigureAwait(false);

            // Publish events
            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Stock set successfully for product {ProductId}", command.ProductId);

            return new SetStockResponse { Success = true };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid stock set request: {ProductId}", command.ProductId);
            return new SetStockResponse { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for product {ProductId}", command.ProductId);
            return new SetStockResponse { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting stock for product {ProductId}", command.ProductId);
            return new SetStockResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region ValidateStockCommand

/// <summary>
/// Command: Validate if requested quantities are available for multiple products.
/// Command: ValidateStockCommand { items: [ { productId, requestedQty } ] }
/// </summary>
public class ValidateStockCommand : ICommand<ValidateStockResponse>
{
    public List<StockValidationItem> Items { get; set; } = new();
}

public class StockValidationItem
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
}

public class ValidateStockResponse
{
    public bool Success { get; set; }
    public List<StockValidationResult> Results { get; set; } = new();
    public string? Error { get; set; }
}

public class StockValidationResult
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// Handler for ValidateStockCommand.
/// </summary>
public class ValidateStockCommandHandler : ICommandHandler<ValidateStockCommand, ValidateStockResponse>
{
    private readonly IRepository<InventoryItem, string> _inventoryRepository;
    private readonly ILogger<ValidateStockCommandHandler> _logger;

    public ValidateStockCommandHandler(
        IRepository<InventoryItem, string> inventoryRepository,
        ILogger<ValidateStockCommandHandler> logger)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidateStockResponse> HandleAsync(
        ValidateStockCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating stock for {ItemCount} items", command.Items.Count);

            var results = new List<StockValidationResult>();
            var allAvailable = true;

            foreach (var item in command.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId))
                {
                    _logger.LogWarning("Invalid product ID in validation request");
                    results.Add(new StockValidationResult
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.RequestedQuantity,
                        AvailableQuantity = 0,
                        IsAvailable = false
                    });
                    allAvailable = false;
                    continue;
                }

                var inventoryItem = await _inventoryRepository.GetByIdAsync(item.ProductId, cancellationToken)
                    .ConfigureAwait(false);

                var available = inventoryItem?.EnsureAvailable(item.RequestedQuantity) ?? false;
                var availableQty = inventoryItem?.AvailableQuantity ?? 0;

                results.Add(new StockValidationResult
                {
                    ProductId = item.ProductId,
                    RequestedQuantity = item.RequestedQuantity,
                    AvailableQuantity = availableQty,
                    IsAvailable = available
                });

                if (!available)
                    allAvailable = false;
            }

            _logger.LogInformation("Stock validation completed: {AvailableCount}/{TotalCount} items available",
                results.Count(r => r.IsAvailable), results.Count);

            return new ValidateStockResponse
            {
                Success = true,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating stock");
            return new ValidateStockResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region DeductStockForOrderCommand

/// <summary>
/// Command: Deduct stock for an order (atomic and idempotent per order).
/// Command: DeductStockForOrderCommand { orderId, items: [ { productId, qty } ] }
/// </summary>
public class DeductStockForOrderCommand : ICommand<DeductStockForOrderResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public List<StockDeductionItem> Items { get; set; } = new();
}

public class StockDeductionItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class DeductStockForOrderResponse
{
    public bool Success { get; set; }
    public List<DeductionResult> Results { get; set; } = new();
    public string? Error { get; set; }
}

public class DeductionResult
{
    public string ProductId { get; set; } = string.Empty;
    public int QuantityDeducted { get; set; }
    public int RemainingQuantity { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for DeductStockForOrderCommand.
/// Handles deductions atomically per order and ensures idempotency.
/// </summary>
public class DeductStockForOrderCommandHandler : ICommandHandler<DeductStockForOrderCommand, DeductStockForOrderResponse>
{
    private readonly IRepository<InventoryItem, string> _inventoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DeductStockForOrderCommandHandler> _logger;

    public DeductStockForOrderCommandHandler(
        IRepository<InventoryItem, string> inventoryRepository,
        IEventPublisher eventPublisher,
        ILogger<DeductStockForOrderCommandHandler> logger)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeductStockForOrderResponse> HandleAsync(
        DeductStockForOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deducting stock for order {OrderId} with {ItemCount} items",
                command.OrderId, command.Items.Count);

            if (string.IsNullOrWhiteSpace(command.OrderId))
                return new DeductStockForOrderResponse { Success = false, Error = "OrderId is required" };

            if (command.Items == null || command.Items.Count == 0)
                return new DeductStockForOrderResponse { Success = false, Error = "Items list cannot be empty" };

            var results = new List<DeductionResult>();
            var envelopes = new List<DomainEventEnvelope>();

            // First pass: Validate all items can be deducted (fail-fast before any saves)
            var itemsToProcess = new Dictionary<string, (int quantity, InventoryItem item)>();
            
            foreach (var item in command.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId))
                {
                    results.Add(new DeductionResult
                    {
                        ProductId = item.ProductId,
                        Success = false,
                        Error = "ProductId is required"
                    });
                    continue;
                }

                try
                {
                    var inventoryItem = await _inventoryRepository.GetByIdAsync(item.ProductId, cancellationToken)
                        .ConfigureAwait(false);

                    if (inventoryItem == null)
                    {
                        results.Add(new DeductionResult
                        {
                            ProductId = item.ProductId,
                            Success = false,
                            Error = "Product not found in inventory"
                        });
                        continue;
                    }

                    // Validate availability first (fail-fast)
                    if (!inventoryItem.EnsureAvailable(item.Quantity))
                    {
                        results.Add(new DeductionResult
                        {
                            ProductId = item.ProductId,
                            Success = false,
                            Error = $"Insufficient inventory for product {item.ProductId}: requested {item.Quantity}, available {inventoryItem.AvailableQuantity}"
                        });
                        continue;
                    }

                    // Store for batch processing after all validations pass
                    itemsToProcess[item.ProductId] = (item.Quantity, inventoryItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating inventory for order {OrderId}, product {ProductId}",
                        command.OrderId, item.ProductId);

                    results.Add(new DeductionResult
                    {
                        ProductId = item.ProductId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            // Second pass: Perform actual deductions and collect events
            foreach (var item in command.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId))
                    continue;

                try
                {
                    if (!itemsToProcess.TryGetValue(item.ProductId, out var deductionInfo))
                        continue; // Already handled in first pass

                    var (quantity, inventoryItem) = deductionInfo;
                    inventoryItem.DeductForOrder(command.OrderId, quantity, command.CheckoutId);

                    // Capture events
                    var itemEnvelopes = inventoryItem.UncommittedEvents
                        .Select(evt => new DomainEventEnvelope(
                            evt,
                            string.IsNullOrEmpty(command.CheckoutId) ? Guid.NewGuid().ToString() : command.CheckoutId,
                            null,
                            null,
                            null))
                        .ToList();

                    envelopes.AddRange(itemEnvelopes);

                    // Save aggregate
                    await _inventoryRepository.SaveAsync(inventoryItem, cancellationToken).ConfigureAwait(false);

                    // Update result with actual deduction
                    var resultIndex = results.FindIndex(r => r.ProductId == item.ProductId);
                    if (resultIndex >= 0)
                    {
                        results[resultIndex] = new DeductionResult
                        {
                            ProductId = item.ProductId,
                            QuantityDeducted = quantity,
                            RemainingQuantity = inventoryItem.AvailableQuantity,
                            Success = true
                        };
                    }

                    _logger.LogInformation("Stock deducted for order {OrderId}, product {ProductId}: {Quantity}",
                        command.OrderId, item.ProductId, quantity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deducting stock for order {OrderId}, product {ProductId}",
                        command.OrderId, item.ProductId);

                    var resultIndex = results.FindIndex(r => r.ProductId == item.ProductId);
                    if (resultIndex >= 0)
                    {
                        results[resultIndex] = new DeductionResult
                        {
                            ProductId = item.ProductId,
                            Success = false,
                            Error = ex.Message
                        };
                    }
                }
            }

            // Publish all events
            if (envelopes.Count > 0)
                await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            var allSuccessful = results.All(r => r.Success);
            _logger.LogInformation("Stock deduction completed for order {OrderId}: {SuccessCount}/{TotalCount}",
                command.OrderId, results.Count(r => r.Success), results.Count);

            return new DeductStockForOrderResponse
            {
                Success = allSuccessful,
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing deduction for order {OrderId}", command.OrderId);
            return new DeductStockForOrderResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: ValidateStockBatchCommand (validate stock availability for checkout saga)
/// </summary>
public class ValidateStockBatchCommand : ICommand<ValidateStockBatchResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public List<StockValidationItem> Items { get; set; } = [];
}

public class StockValidationItem
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
}

public class ValidateStockBatchResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for ValidateStockBatchCommand
/// Validates stock for all items and publishes StockBatchValidatedEvent
/// </summary>
public class ValidateStockBatchCommandHandler : ICommandHandler<ValidateStockBatchCommand, ValidateStockBatchResponse>
{
    private readonly IRepository<InventoryItem, string> _inventoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ValidateStockBatchCommandHandler> _logger;

    public ValidateStockBatchCommandHandler(
        IRepository<InventoryItem, string> inventoryRepository,
        IEventPublisher eventPublisher,
        ILogger<ValidateStockBatchCommandHandler> logger)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValidateStockBatchResponse> HandleAsync(ValidateStockBatchCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.CheckoutId))
            return new ValidateStockBatchResponse { Success = false, Error = "CheckoutId is required" };

        if (command.Items == null || command.Items.Count == 0)
            return new ValidateStockBatchResponse { Success = false, Error = "Items are required" };

        try
        {
            var results = new List<StockValidationResult>();
            var allAvailable = true;

            // Validate each product
            foreach (var item in command.Items)
            {
                var inventory = await _inventoryRepository.GetByIdAsync(item.ProductId, cancellationToken);
                var available = inventory?.AvailableQuantity >= item.RequestedQuantity;

                results.Add(new StockValidationResult
                {
                    ProductId = item.ProductId,
                    RequestedQuantity = item.RequestedQuantity,
                    AvailableQuantity = inventory?.AvailableQuantity ?? 0,
                    IsAvailable = available ?? false
                });

                if (!available)
                    allAvailable = false;
            }

            // Publish validation result
            await _eventPublisher.PublishAsync(new[]
            {
                new DomainEventEnvelope(
                    new StockBatchValidatedEvent
                    {
                        AggregateId = command.CheckoutId,
                        CheckoutId = command.CheckoutId,
                        AllAvailable = allAvailable,
                        Results = results
                    },
                    command.CheckoutId)
            }, cancellationToken);

            _logger.LogInformation("Stock batch validated for CheckoutId {CheckoutId}: {AvailableCount}/{TotalCount} items available",
                command.CheckoutId, results.Count(r => r.IsAvailable), results.Count);

            return new ValidateStockBatchResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating stock batch for CheckoutId {CheckoutId}", command.CheckoutId);
            return new ValidateStockBatchResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion
