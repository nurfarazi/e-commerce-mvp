using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Checkout.Domain;

/// <summary>
/// Domain event: Checkout saga was initiated.
/// </summary>
public class CheckoutSagaInitiatedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public CustomerInfoDto CustomerInfo { get; set; } = null!;
    public ShippingAddressDto ShippingAddress { get; set; } = null!;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Cart snapshot was provided to saga.
/// </summary>
public class CartSnapshotReceivedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public List<CartItemSnapshotDto> CartItems { get; set; } = [];
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product snapshots were provided to saga.
/// </summary>
public class ProductSnapshotsReceivedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public List<ProductSnapshotDto> ProductSnapshots { get; set; } = [];
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Stock validation completed.
/// </summary>
public class StockValidationCompletedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public bool AllAvailable { get; set; }
    public List<StockValidationResultDto> Results { get; set; } = [];
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Stock was deducted for order.
/// </summary>
public class StockDeductedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Order was created.
/// </summary>
public class OrderCreatedInSagaEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Cart was cleared.
/// </summary>
public class CartClearedInSagaEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Order was finalized.
/// </summary>
public class OrderFinalizedInSagaEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Checkout saga failed.
/// </summary>
public class CheckoutSagaFailedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string FailedAt { get; set; } = string.Empty;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Checkout saga completed successfully.
/// </summary>
public class CheckoutSagaCompletedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public override int EventVersion => 1;
}
