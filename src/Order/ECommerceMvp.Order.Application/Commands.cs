using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Domain;
using OrderAggregate = ECommerceMvp.Order.Domain.Order;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Order.Application;

#region PlaceOrderCommand

/// <summary>
/// Command: Place an order from a shopping cart.
/// Command: PlaceOrderCommand { guestToken, cartId, customerInfo, address, idempotencyKey }
/// </summary>
public class PlaceOrderCommand : ICommand<PlaceOrderResponse>
{
    public string OrderId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public CustomerInfoRequest CustomerInfo { get; set; } = null!;
    public ShippingAddressRequest ShippingAddress { get; set; } = null!;
    public List<CartItemSnapshot> CartItems { get; set; } = [];
    public List<ProductSnapshot> ProductSnapshots { get; set; } = [];
}

public class CustomerInfoRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class ShippingAddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";
}

public class CartItemSnapshot
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class ProductSnapshot
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
}

public class PlaceOrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for PlaceOrderCommand.
/// Validates invariants: cart not empty, all products active, idempotency.
/// </summary>
public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, PlaceOrderResponse>
{
    private readonly IRepository<OrderAggregate, string> _orderRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;

    public PlaceOrderCommandHandler(
        IRepository<OrderAggregate, string> orderRepository,
        IEventPublisher eventPublisher,
        IIdempotencyStore idempotencyStore,
        ILogger<PlaceOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlaceOrderResponse> HandleAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Placing order {OrderId} with idempotency key {IdempotencyKey}", 
                command.OrderId, command.IdempotencyKey);

            // Validate command
            var validationError = ValidateCommand(command);
            if (!string.IsNullOrEmpty(validationError))
                return new PlaceOrderResponse { Success = false, Error = validationError };

            // Check idempotency: have we seen this key before?
            var idempotencyResult = await _idempotencyStore.CheckIdempotencyAsync(
                command.IdempotencyKey, 
                cancellationToken).ConfigureAwait(false);

            if (idempotencyResult.IsIdempotent)
            {
                // We've seen this key before - return cached result or reject
                _logger.LogWarning("Idempotency key {IdempotencyKey} already processed", command.IdempotencyKey);
                
                // Check if the cart contents match (if we're storing that info)
                // For now, return the previously created order
                return new PlaceOrderResponse 
                { 
                    Success = false, 
                    Error = "IDEMPOTENCY_CONFLICT: Order already placed with this key" 
                };
            }

            // Validate invariants
            var invariantError = ValidateInvariants(command);
            if (!string.IsNullOrEmpty(invariantError))
                return new PlaceOrderResponse { Success = false, Error = invariantError };

            // Create line items from cart snapshots
            var lineItems = CreateLineItems(command);
            if (lineItems.Count == 0)
                return new PlaceOrderResponse { Success = false, Error = "Cart is empty" };

            // Calculate subtotal
            var subtotal = lineItems
                .Aggregate(new Money(0, "USD"), (acc, item) => acc + item.LineTotal);

            // Create customer info and address value objects
            var customerInfo = new CustomerInfo(
                command.CustomerInfo.Name,
                command.CustomerInfo.Phone,
                command.CustomerInfo.Email);

            var shippingAddress = new ShippingAddress(
                command.ShippingAddress.Line1,
                command.ShippingAddress.City,
                command.ShippingAddress.Line2,
                command.ShippingAddress.PostalCode,
                command.ShippingAddress.Country);

            // Create order aggregate
            var orderNumber = GenerateOrderNumber();
            var order = OrderAggregate.PlaceFromCart(
                command.OrderId,
                orderNumber,
                new GuestToken(command.GuestToken),
                command.CartId,
                lineItems,
                customerInfo,
                shippingAddress,
                subtotal);

            // Emit domain events before capturing envelopes
            EmitOrderEvents(order, command.IdempotencyKey);

            var envelopes = order.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(
                    evt,
                    Guid.NewGuid().ToString(), // correlationId
                    command.IdempotencyKey,    // causationId
                    command.GuestToken,        // tenantId
                    null))                     // userId
                .ToList();

            // Save aggregate
            await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

            // Mark idempotency key as processed
            await _idempotencyStore.MarkIdempotencyProcessedAsync(
                command.IdempotencyKey,
                command.OrderId,
                cancellationToken).ConfigureAwait(false);

            // Publish events
            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Order {OrderId} ({OrderNumber}) placed successfully", 
                command.OrderId, orderNumber);

            return new PlaceOrderResponse 
            { 
                OrderId = command.OrderId, 
                OrderNumber = orderNumber,
                Success = true 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing order {OrderId}", command.OrderId);
            return new PlaceOrderResponse { Success = false, Error = $"Internal error: {ex.Message}" };
        }
    }

    private string ValidateCommand(PlaceOrderCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.OrderId))
            return "OrderId is required";

        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return "GuestToken is required";

        if (string.IsNullOrWhiteSpace(command.CartId))
            return "CartId is required";

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            return "IdempotencyKey is required";

        if (command.CustomerInfo == null)
            return "CustomerInfo is required";

        if (string.IsNullOrWhiteSpace(command.CustomerInfo.Name))
            return "Customer name is required";

        if (string.IsNullOrWhiteSpace(command.CustomerInfo.Phone))
            return "Customer phone is required";

        if (command.ShippingAddress == null)
            return "ShippingAddress is required";

        if (string.IsNullOrWhiteSpace(command.ShippingAddress.Line1))
            return "Address line 1 is required";

        if (string.IsNullOrWhiteSpace(command.ShippingAddress.City))
            return "City is required";

        if (command.CartItems == null || command.CartItems.Count == 0)
            return "Cart is empty";

        if (command.ProductSnapshots == null || command.ProductSnapshots.Count == 0)
            return "Product snapshots are required";

        return string.Empty;
    }

    private string ValidateInvariants(PlaceOrderCommand command)
    {
        // Validate: all products must be active
        foreach (var cartItem in command.CartItems)
        {
            var product = command.ProductSnapshots.FirstOrDefault(p => p.ProductId == cartItem.ProductId);
            if (product == null)
                return $"Product {cartItem.ProductId} not found in snapshots";

            if (!product.IsActive)
                return $"Product {product.ProductId} ({product.Sku}) is not active";
        }

        // Note: Actual inventory quantity validation happens during stock commit
        // This is just ensuring products exist and are active

        return string.Empty;
    }

    private List<OrderLineItem> CreateLineItems(PlaceOrderCommand command)
    {
        var lineItems = new List<OrderLineItem>();

        foreach (var cartItem in command.CartItems)
        {
            var product = command.ProductSnapshots.First(p => p.ProductId == cartItem.ProductId);
            
            var lineItem = new OrderLineItem(
                Guid.NewGuid().ToString(),
                product.ProductId,
                product.Sku,
                product.Name,
                new Money(product.Price, product.Currency),
                cartItem.Quantity);

            lineItems.Add(lineItem);
        }

        return lineItems;
    }

    private string GenerateOrderNumber()
    {
        // Generate human-readable order number: ORD-YYYYMMDD-XXXXX
        var timestamp = DateTime.UtcNow;
        var random = new Random();
        var randomPart = random.Next(10000, 99999);
        return $"ORD-{timestamp:yyyyMMdd}-{randomPart}";
    }

    private void EmitOrderEvents(OrderAggregate order, string idempotencyKey)
    {
        // Emit OrderPlacementRequested (optional, for audit trail)
        var placementRequestedEvent = new OrderPlacementRequestedEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            GuestToken = order.GuestToken.Value,
            CartId = order.CartId,
            IdempotencyKey = idempotencyKey,
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(placementRequestedEvent);

        // Emit OrderValidated
        var validatedEvent = new OrderValidatedEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(validatedEvent);

        // Emit OrderPriced
        var pricedEvent = new OrderPricedEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            ItemsPriced = order.LineItems.Select(li => new PricedItem
            {
                ProductId = li.ProductId,
                Sku = li.SkuSnapshot,
                Name = li.NameSnapshot,
                UnitPrice = li.UnitPriceSnapshot.Amount,
                Quantity = li.Quantity,
                LineTotal = li.LineTotal.Amount
            }).ToList(),
            Subtotal = order.Totals.Subtotal.Amount,
            ShippingFee = order.Totals.ShippingFee.Amount,
            Total = order.Totals.Total.Amount,
            Currency = order.Totals.Subtotal.Currency,
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(pricedEvent);

        // Emit OrderCreated
        var createdEvent = new OrderCreatedEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber.Value,
            GuestToken = order.GuestToken.Value,
            CartId = order.CartId,
            CustomerInfo = new CustomerInfoDto
            {
                Name = order.CustomerInfo.Name,
                Phone = order.CustomerInfo.Phone,
                Email = order.CustomerInfo.Email
            },
            ShippingAddress = new ShippingAddressDto
            {
                Line1 = order.ShippingAddress.Line1,
                Line2 = order.ShippingAddress.Line2,
                City = order.ShippingAddress.City,
                PostalCode = order.ShippingAddress.PostalCode,
                Country = order.ShippingAddress.Country
            },
            Totals = new OrderTotalsDto
            {
                Subtotal = order.Totals.Subtotal.Amount,
                ShippingFee = order.Totals.ShippingFee.Amount,
                Total = order.Totals.Total.Amount,
                Currency = order.Totals.Subtotal.Currency
            },
            LineItems = order.LineItems.Select(li => new OrderLineItemDto
            {
                LineItemId = li.Id,
                ProductId = li.ProductId,
                SkuSnapshot = li.SkuSnapshot,
                NameSnapshot = li.NameSnapshot,
                UnitPriceSnapshot = li.UnitPriceSnapshot.Amount,
                Quantity = li.Quantity,
                LineTotal = li.LineTotal.Amount
            }).ToList(),
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            CreatedAt = order.CreatedAt,
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(createdEvent);

        // Emit OrderStockCommitRequested
        var stockCommitRequestedEvent = new OrderStockCommitRequestedEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            Items = order.LineItems.Select(li => new StockCommitItem
            {
                ProductId = li.ProductId,
                Quantity = li.Quantity
            }).ToList(),
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(stockCommitRequestedEvent);

        // Emit OrderSubmitted (cross-context integration event)
        var submittedEvent = new OrderSubmittedIntegrationEvent
        {
            AggregateId = order.Id,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber.Value,
            GuestToken = order.GuestToken.Value,
            Items = order.LineItems.Select(li => new OrderLineItemDto
            {
                LineItemId = li.Id,
                ProductId = li.ProductId,
                SkuSnapshot = li.SkuSnapshot,
                NameSnapshot = li.NameSnapshot,
                UnitPriceSnapshot = li.UnitPriceSnapshot.Amount,
                Quantity = li.Quantity,
                LineTotal = li.LineTotal.Amount
            }).ToList(),
            Totals = new OrderTotalsDto
            {
                Subtotal = order.Totals.Subtotal.Amount,
                ShippingFee = order.Totals.ShippingFee.Amount,
                Total = order.Totals.Total.Amount,
                Currency = order.Totals.Subtotal.Currency
            },
            Timestamp = DateTime.UtcNow
        };

        order.AddUncommittedEvent(submittedEvent);
    }
}

#endregion
