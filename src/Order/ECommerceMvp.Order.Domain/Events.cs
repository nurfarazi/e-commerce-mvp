using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Order.Domain;

/// <summary>
/// Domain event: Order placement was requested (optional initial event).
/// Event: OrderPlacementRequested { orderId, guestToken, cartId, idempotencyKey }
/// </summary>
public class OrderPlacementRequestedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Order was validated (all invariants checked).
/// Event: OrderValidated { orderId }
/// </summary>
public class OrderValidatedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Order was priced with line items and totals.
/// Event: OrderPriced { orderId, itemsPriced, subtotal, shippingFee=0, total }
/// </summary>
public class OrderPricedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public List<PricedItem> ItemsPriced { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; } = 0;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

public class PricedItem
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Domain event: Order was created successfully.
/// Event: OrderCreated { orderId, orderNumber, guestToken, customerInfo, address, totals, paymentMethod=COD, paymentStatus=Pending }
/// </summary>
public class OrderCreatedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public CustomerInfoDto CustomerInfo { get; set; } = null!;
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public OrderTotalsDto Totals { get; set; } = null!;
    public List<OrderLineItemDto> LineItems { get; set; } = [];
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

public class CustomerInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class ShippingAddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";
}

public class OrderTotalsDto
{
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; } = 0;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
}

public class OrderLineItemDto
{
    public string LineItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string SkuSnapshot { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Domain event: Stock commitment was requested for the order.
/// Event: OrderStockCommitRequested { orderId, items: [productId, qty] }
/// </summary>
public class OrderStockCommitRequestedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public List<StockCommitItem> Items { get; set; } = [];
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

public class StockCommitItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// Domain event: Stock was successfully committed for the order.
/// Event: OrderStockCommitted { orderId }
/// </summary>
public class OrderStockCommittedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Cart clear was requested for the order (cleanup).
/// Event: OrderCartClearRequested { orderId, cartId }
/// </summary>
public class OrderCartClearRequestedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Cart was cleared after order creation.
/// Event: OrderCartCleared { orderId, cartId }
/// </summary>
public class OrderCartClearedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Order was finalized and ready for processing.
/// Event: OrderFinalized { orderId }
/// </summary>
public class OrderFinalizedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Cross-context integration event: Order was submitted and visible to other services.
/// Published to event bus for external subscribers (Inventory, Cart, etc.).
/// Event: OrderSubmitted { orderId, orderNumber, guestToken, items, totals }
/// </summary>
public class OrderSubmittedIntegrationEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public List<OrderLineItemDto> Items { get; set; } = [];
    public OrderTotalsDto Totals { get; set; } = null!;
    public DateTime Timestamp { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Cross-context integration event: Request Inventory service to commit stock.
/// Published to Inventory event bus.
/// Event: StockCommitRequested { orderId, items }
/// </summary>
public class StockCommitRequestedIntegrationEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public List<StockCommitItem> Items { get; set; } = [];

    public override int EventVersion => 1;
}

/// <summary>
/// Cross-context integration event: Request Cart service to clear guest cart.
/// Published to Cart event bus.
/// Event: CartClearRequested { guestToken, cartId }
/// </summary>
public class CartClearRequestedIntegrationEvent : DomainEvent
{
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}
