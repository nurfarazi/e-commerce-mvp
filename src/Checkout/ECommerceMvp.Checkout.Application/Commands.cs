using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Checkout.Domain;
using Microsoft.Extensions.Logging;
using CheckoutSagaAggregate = ECommerceMvp.Checkout.Domain.CheckoutSaga;

namespace ECommerceMvp.Checkout.Application;

#region InitiateCheckoutCommand

/// <summary>
/// Command: Initiate a new checkout saga.
/// </summary>
public class InitiateCheckoutCommand : ICommand<InitiateCheckoutResponse>
{
    public string CheckoutId { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public CustomerInfoDto CustomerInfo { get; set; } = null!;
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
}

public class InitiateCheckoutResponse
{
    public string CheckoutId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for InitiateCheckoutCommand.
/// Creates a new CheckoutSaga and sends GetCartSnapshotCommand to Cart service.
/// </summary>
public class InitiateCheckoutCommandHandler : ICommandHandler<InitiateCheckoutCommand, InitiateCheckoutResponse>
{
    private readonly IRepository<CheckoutSagaAggregate, string> _sagaRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<InitiateCheckoutCommandHandler> _logger;

    public InitiateCheckoutCommandHandler(
        IRepository<CheckoutSagaAggregate, string> sagaRepository,
        IEventPublisher eventPublisher,
        ICommandEnqueuer commandEnqueuer,
        IIdempotencyStore idempotencyStore,
        ILogger<InitiateCheckoutCommandHandler> logger)
    {
        _sagaRepository = sagaRepository;
        _eventPublisher = eventPublisher;
        _commandEnqueuer = commandEnqueuer;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    public async Task<InitiateCheckoutResponse> HandleAsync(InitiateCheckoutCommand command)
    {
        try
        {
            // Check idempotency
            var existingResult = await _idempotencyStore.GetResultAsync(command.IdempotencyKey);
            if (existingResult != null)
            {
                _logger.LogInformation("Duplicate InitiateCheckout request with idempotency key {Key}",
                    command.IdempotencyKey);
                return new InitiateCheckoutResponse { CheckoutId = command.CheckoutId, Success = true };
            }

            // Create CheckoutSaga aggregate
            var saga = CheckoutSagaAggregate.Initiate(
                command.CheckoutId,
                command.OrderId,
                command.GuestToken,
                command.CartId,
                command.CustomerInfo,
                command.ShippingAddress);

            // Save saga
            await _sagaRepository.SaveAsync(saga);
            _logger.LogInformation("CheckoutSaga initiated: {CheckoutId}", command.CheckoutId);

            // Publish saga events
            await _eventPublisher.PublishAsync(saga.UncommittedEvents);

            // Send GetCartSnapshotCommand to Cart service
            await _commandEnqueuer.EnqueueAsync(
                new GetCartSnapshotCommand
                {
                    CheckoutId = command.CheckoutId,
                    GuestToken = command.GuestToken,
                    CartId = command.CartId
                },
                "cart.commands");

            _logger.LogInformation("GetCartSnapshotCommand sent for CheckoutId {CheckoutId}",
                command.CheckoutId);

            // Store idempotency result
            var response = new InitiateCheckoutResponse { CheckoutId = command.CheckoutId, Success = true };
            await _idempotencyStore.StoreResultAsync(command.IdempotencyKey, response);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating checkout: {CheckoutId}", command.CheckoutId);
            return new InitiateCheckoutResponse
            {
                CheckoutId = command.CheckoutId,
                Success = false,
                Error = ex.Message
            };
        }
    }
}

#endregion

#region GetCartSnapshotCommand

/// <summary>
/// Command: Get cart snapshot from Cart service.
/// Sent by Checkout orchestrator to Cart service.
/// </summary>
public class GetCartSnapshotCommand : ICommand<GetCartSnapshotResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
}

public class GetCartSnapshotResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

#endregion

#region AdvanceSagaCommand

/// <summary>
/// Command: Advance the checkout saga based on received event.
/// This command is internal to Checkout and processes events from other services.
/// </summary>
public class AdvanceSagaCommand : ICommand<AdvanceSagaResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object EventPayload { get; set; } = null!;
}

public class AdvanceSagaResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for AdvanceSagaCommand.
/// Routes incoming events and orchestrates next steps in the saga.
/// </summary>
public class AdvanceSagaCommandHandler : ICommandHandler<AdvanceSagaCommand, AdvanceSagaResponse>
{
    private readonly IRepository<CheckoutSagaAggregate, string> _sagaRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly ILogger<AdvanceSagaCommandHandler> _logger;

    public AdvanceSagaCommandHandler(
        IRepository<CheckoutSagaAggregate, string> sagaRepository,
        IEventPublisher eventPublisher,
        ICommandEnqueuer commandEnqueuer,
        ILogger<AdvanceSagaCommandHandler> logger)
    {
        _sagaRepository = sagaRepository;
        _eventPublisher = eventPublisher;
        _commandEnqueuer = commandEnqueuer;
        _logger = logger;
    }

    public async Task<AdvanceSagaResponse> HandleAsync(AdvanceSagaCommand command)
    {
        try
        {
            // Load CheckoutSaga
            var saga = await _sagaRepository.GetByIdAsync(command.CheckoutId);
            if (saga == null)
            {
                _logger.LogError("CheckoutSaga not found: {CheckoutId}", command.CheckoutId);
                return new AdvanceSagaResponse { Success = false, Error = "Saga not found" };
            }

            // Route event and send next command
            switch (command.EventType)
            {
                case "CartSnapshotProvidedEvent":
                    await HandleCartSnapshotProvided(saga, command.EventPayload);
                    break;

                case "CartSnapshotFailedEvent":
                    saga.Fail("Cart snapshot failed", "CartSnapshot");
                    break;

                case "ProductSnapshotsProvidedEvent":
                    await HandleProductSnapshotsProvided(saga, command.EventPayload);
                    break;

                case "ProductSnapshotFailedEvent":
                    saga.Fail("Product snapshots failed", "ProductSnapshot");
                    break;

                case "StockBatchValidatedEvent":
                    await HandleStockValidated(saga, command.EventPayload);
                    break;

                case "StockDeductedForOrderEvent":
                    await HandleStockDeducted(saga, command.EventPayload);
                    break;

                case "OrderCreatedEvent":
                    await HandleOrderCreated(saga, command.EventPayload);
                    break;

                case "CartClearedEvent":
                    await HandleCartCleared(saga, command.EventPayload);
                    break;

                case "OrderFinalizedEvent":
                    await HandleOrderFinalized(saga, command.EventPayload);
                    break;

                default:
                    _logger.LogWarning("Unknown event type for saga advancement: {EventType}",
                        command.EventType);
                    break;
            }

            // Save saga and publish events
            await _sagaRepository.SaveAsync(saga);
            await _eventPublisher.PublishAsync(saga.UncommittedEvents);

            return new AdvanceSagaResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing saga: {CheckoutId}", command.CheckoutId);
            return new AdvanceSagaResponse { Success = false, Error = ex.Message };
        }
    }

    private async Task HandleCartSnapshotProvided(CheckoutSagaAggregate saga, object payload)
    {
        var cartItems = (List<CartItemSnapshotDto>?)payload ?? [];
        saga.HandleCartSnapshotProvided(cartItems);

        // Next step: Get product snapshots
        var productIds = cartItems.Select(ci => ci.ProductId).ToList();
        await _commandEnqueuer.EnqueueAsync(
            new GetProductSnapshotsCommand
            {
                CheckoutId = saga.CheckoutId,
                ProductIds = productIds
            },
            "productcatalog.commands");

        _logger.LogInformation("Cart snapshot received for {CheckoutId}, requesting product snapshots",
            saga.CheckoutId);
    }

    private async Task HandleProductSnapshotsProvided(CheckoutSagaAggregate saga, object payload)
    {
        var products = (List<ProductSnapshotDto>?)payload ?? [];
        saga.HandleProductSnapshotsProvided(products);

        // Next step: Validate stock
        var items = saga.CartItems.Select(ci => new StockValidationItem
        {
            ProductId = ci.ProductId,
            RequestedQuantity = ci.Quantity
        }).ToList();

        await _commandEnqueuer.EnqueueAsync(
            new ValidateStockBatchCommand
            {
                CheckoutId = saga.CheckoutId,
                Items = items
            },
            "inventory.commands");

        _logger.LogInformation("Product snapshots received for {CheckoutId}, validating stock",
            saga.CheckoutId);
    }

    private async Task HandleStockValidated(CheckoutSagaAggregate saga, object payload)
    {
        var validationResult = (StockValidationResult?)payload;
        bool allAvailable = validationResult?.AllAvailable ?? false;
        var results = validationResult?.Results ?? [];

        saga.HandleStockValidated(allAvailable, results);

        if (allAvailable)
        {
            // Next step: Deduct stock
            var items = saga.CartItems.Select(ci => new StockDeductionItem
            {
                ProductId = ci.ProductId,
                Quantity = ci.Quantity
            }).ToList();

            await _commandEnqueuer.EnqueueAsync(
                new DeductStockForOrderCommand
                {
                    CheckoutId = saga.CheckoutId,
                    OrderId = saga.OrderId,
                    Items = items
                },
                "inventory.commands");

            _logger.LogInformation("Stock validated for {CheckoutId}, deducting stock",
                saga.CheckoutId);
        }
        else
        {
            _logger.LogWarning("Stock validation failed for {CheckoutId}", saga.CheckoutId);
        }
    }

    private async Task HandleStockDeducted(CheckoutSagaAggregate saga, object payload)
    {
        saga.HandleStockDeducted();

        // Next step: Create order
        await _commandEnqueuer.EnqueueAsync(
            new CreateOrderCommand
            {
                CheckoutId = saga.CheckoutId,
                OrderId = saga.OrderId,
                GuestToken = saga.GuestToken,
                CartId = saga.CartId,
                IdempotencyKey = saga.CheckoutId,
                CustomerInfo = saga.CustomerInfo,
                ShippingAddress = saga.ShippingAddress,
                CartItems = saga.CartItems,
                ProductSnapshots = saga.ProductSnapshots
            },
            "order.commands");

        _logger.LogInformation("Stock deducted for {CheckoutId}, creating order", saga.CheckoutId);
    }

    private async Task HandleOrderCreated(CheckoutSagaAggregate saga, object payload)
    {
        var orderInfo = (OrderCreatedInfo?)payload;
        var orderNumber = orderInfo?.OrderNumber ?? string.Empty;
        saga.HandleOrderCreated(orderNumber);

        // Next step: Clear cart
        await _commandEnqueuer.EnqueueAsync(
            new ClearCartCommand
            {
                CheckoutId = saga.CheckoutId,
                GuestToken = saga.GuestToken
            },
            "cart.commands");

        _logger.LogInformation("Order created for {CheckoutId}, clearing cart", saga.CheckoutId);
    }

    private async Task HandleCartCleared(CheckoutSagaAggregate saga, object payload)
    {
        saga.HandleCartCleared();

        // Next step: Finalize order
        await _commandEnqueuer.EnqueueAsync(
            new FinalizeOrderCommand
            {
                CheckoutId = saga.CheckoutId,
                OrderId = saga.OrderId
            },
            "order.commands");

        _logger.LogInformation("Cart cleared for {CheckoutId}, finalizing order", saga.CheckoutId);
    }

    private async Task HandleOrderFinalized(CheckoutSagaAggregate saga, object payload)
    {
        saga.HandleOrderFinalized();
        _logger.LogInformation("Order finalized for {CheckoutId}, saga completed", saga.CheckoutId);
    }
}

#endregion

#region Supporting Commands

/// <summary>
/// Command: Get product snapshots from ProductCatalog service.
/// </summary>
public class GetProductSnapshotsCommand : ICommand<GetProductSnapshotsResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = [];
}

public class GetProductSnapshotsResponse
{
    public bool Success { get; set; }
}

/// <summary>
/// Command: Validate stock batch in Inventory service.
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
}

public class StockValidationResult
{
    public bool AllAvailable { get; set; }
    public List<StockValidationResultDto> Results { get; set; } = [];
}

/// <summary>
/// Command: Deduct stock for order in Inventory service.
/// </summary>
public class DeductStockForOrderCommand : ICommand<DeductStockForOrderResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public List<StockDeductionItem> Items { get; set; } = [];
}

public class StockDeductionItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class DeductStockForOrderResponse
{
    public bool Success { get; set; }
}

/// <summary>
/// Command: Create order in Order service.
/// </summary>
public class CreateOrderCommand : ICommand<CreateOrderResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public CustomerInfoDto CustomerInfo { get; set; } = null!;
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public List<CartItemSnapshotDto> CartItems { get; set; } = [];
    public List<ProductSnapshotDto> ProductSnapshots { get; set; } = [];
}

public class CreateOrderResponse
{
    public bool Success { get; set; }
}

public class OrderCreatedInfo
{
    public string OrderNumber { get; set; } = string.Empty;
}

/// <summary>
/// Command: Clear cart in Cart service.
/// </summary>
public class ClearCartCommand : ICommand<ClearCartResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
}

public class ClearCartResponse
{
    public bool Success { get; set; }
}

/// <summary>
/// Command: Finalize order in Order service.
/// </summary>
public class FinalizeOrderCommand : ICommand<FinalizeOrderResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
}

public class FinalizeOrderResponse
{
    public bool Success { get; set; }
}

#endregion
