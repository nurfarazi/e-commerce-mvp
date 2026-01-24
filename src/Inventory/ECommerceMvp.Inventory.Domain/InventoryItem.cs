using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Inventory.Domain;

/// <summary>
/// Value object for product ID (unique identifier).
/// </summary>
public class ProductId : ValueObject
{
    public string Value { get; }

    public ProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product ID cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProductId productId) => productId.Value;
    public static implicit operator ProductId(string value) => new(value);
}

/// <summary>
/// Value object for stock quantity.
/// Constraints: Quantity >= 0
/// </summary>
public class Quantity : ValueObject
{
    public int Amount { get; }

    public Quantity(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(amount));

        Amount = amount;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
    }

    public override string ToString() => Amount.ToString();

    public static implicit operator int(Quantity quantity) => quantity.Amount;
    public static implicit operator Quantity(int amount) => new(amount);
}

/// <summary>
/// Value object for adjustment reason (optional, admin-provided).
/// </summary>
public class AdjustmentReason : ValueObject
{
    public string? Value { get; }

    public AdjustmentReason(string? value = null)
    {
        Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value ?? "(no reason)";

    public static implicit operator string?(AdjustmentReason reason) => reason.Value;
    public static implicit operator AdjustmentReason(string? value) => new(value);
}

/// <summary>
/// InventoryItem aggregate root in the Inventory bounded context.
/// Manages stock for a single product.
/// 
/// Invariants:
/// - StockQuantity >= 0 (never negative)
/// - Deductions are atomic and idempotent per order
/// - Product must be active in catalog (validated at command boundary)
/// </summary>
public class InventoryItem : AggregateRoot<string>
{
    private InventoryItem(string id) : base(id)
    {
    }

    public string ProductId { get; private set; } = string.Empty;
    public int AvailableQuantity { get; private set; }

    /// <summary>
    /// Create a new inventory item for a product.
    /// Behavior: CreateInventoryItem(productId, initialQty)
    /// </summary>
    public static InventoryItem Create(string productId, int initialQuantity = 0)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be empty", nameof(productId));

        var qty = new Quantity(initialQuantity);

        var item = new InventoryItem(productId);
        var @event = new StockItemCreatedEvent
        {
            ProductId = productId,
            InitialQuantity = qty.Amount
        };

        item.AppendEvent(@event);
        item.ApplyEvent(@event);

        return item;
    }

    /// <summary>
    /// Reconstruct an inventory item from its event history (for event sourcing).
    /// </summary>
    public static InventoryItem FromHistory(string productId)
    {
        return new InventoryItem(productId);
    }

    /// <summary>
    /// Set stock to a new quantity (admin operation).
    /// Behavior: SetStock(newQty, reason?)
    /// </summary>
    public void SetStock(int newQuantity, string? reason = null, string? changedBy = null)
    {
        var qty = new Quantity(newQuantity);
        var adjustmentReason = new AdjustmentReason(reason);
        changedBy = string.IsNullOrWhiteSpace(changedBy) ? null : changedBy.Trim();

        if (AvailableQuantity == qty.Amount)
            throw new InvalidOperationException("New quantity is same as current quantity");

        var @event = new StockSetEvent
        {
            ProductId = ProductId,
            OldQuantity = AvailableQuantity,
            NewQuantity = qty.Amount,
            Reason = adjustmentReason.Value,
            ChangedBy = changedBy
        };

        AppendEvent(@event);
        ApplyEvent(@event);
    }

    /// <summary>
    /// Validate if requested quantity is available (returns success/failure).
    /// Does NOT modify state; used for pre-checks.
    /// </summary>
    public bool EnsureAvailable(int requestedQuantity)
    {
        var qty = new Quantity(requestedQuantity);
        return AvailableQuantity >= qty.Amount;
    }

    /// <summary>
    /// Deduct stock for an order (atomic and idempotent per order).
    /// Throws if insufficient inventory.
    /// Behavior: DeductForOrder(orderId, qty)
    /// </summary>
    public void DeductForOrder(string orderId, int quantityToDeduct, string checkoutId = "")
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID cannot be empty", nameof(orderId));

        var qty = new Quantity(quantityToDeduct);

        if (AvailableQuantity < qty.Amount)
        {
            var rejectionEvent = new StockDeductionRejectedEvent
            {
                OrderId = orderId,
                ProductId = ProductId,
                RequestedQuantity = qty.Amount,
                AvailableQuantity = AvailableQuantity,
                Reason = "Insufficient inventory"
            };

            AppendEvent(rejectionEvent);
            ApplyEvent(rejectionEvent);

            throw new InsufficientInventoryException(
                $"Insufficient inventory for product {ProductId}: requested {qty.Amount}, available {AvailableQuantity}");
        }

        var newQuantity = AvailableQuantity - qty.Amount;
        var @event = new StockDeductedForOrderEvent
        {
            CheckoutId = checkoutId,
            OrderId = orderId,
            ProductId = ProductId,
            QuantityDeducted = qty.Amount,
            OldQuantity = AvailableQuantity,
            NewQuantity = newQuantity
        };

        AppendEvent(@event);
        ApplyEvent(@event);
    }

    public override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case StockItemCreatedEvent created:
                ProductId = created.ProductId;
                AvailableQuantity = created.InitialQuantity;
                break;

            case StockSetEvent stockSet:
                AvailableQuantity = stockSet.NewQuantity;
                break;

            case StockDeductedForOrderEvent deducted:
                AvailableQuantity = deducted.NewQuantity;
                break;

            case StockDeductionRejectedEvent:
                // Event recorded but state not modified (idempotency protection)
                break;
        }
    }
}

/// <summary>
/// Exception thrown when inventory business rule is violated.
/// </summary>
public class InventoryDomainException : DomainException
{
    public InventoryDomainException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when insufficient inventory is available.
/// </summary>
public class InsufficientInventoryException : InventoryDomainException
{
    public InsufficientInventoryException(string message) : base(message) { }
}
