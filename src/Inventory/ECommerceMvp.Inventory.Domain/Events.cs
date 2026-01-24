using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Inventory.Domain;

/// <summary>
/// Domain event: Stock item was created (initialized with initial quantity).
/// Event: StockItemCreated { productId, initialQty }
/// </summary>
public class StockItemCreatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int InitialQuantity { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Stock was set to a new quantity (admin operation).
/// Event: StockSet { productId, oldQty, newQty, reason?, changedBy }
/// </summary>
public class StockSetEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Stock was deducted for an order (atomic per order, idempotent).
/// Event: StockDeductedForOrder { orderId, productId, qty, oldQty, newQty }
/// </summary>
public class StockDeductedForOrderEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int QuantityDeducted { get; set; }
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Stock deduction was rejected (insufficient inventory).
/// Event: StockDeductionRejected { orderId, productId, requestedQty, availableQty, reason }
/// </summary>
public class StockDeductionRejectedEvent : DomainEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Integration event: Stock batch validation completed
/// </summary>
public class StockBatchValidatedEvent : DomainEvent
{
    public string CheckoutId { get; set; } = string.Empty;
    public bool AllAvailable { get; set; }
    public List<StockValidationResult> Results { get; set; } = [];
    public override int EventVersion => 1;
}

/// <summary>
/// DTO: Stock validation result
/// </summary>
public class StockValidationResult
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}
