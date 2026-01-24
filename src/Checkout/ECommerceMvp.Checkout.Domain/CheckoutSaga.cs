using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Checkout.Domain;

/// <summary>
/// Enumeration for checkout saga status.
/// </summary>
public enum CheckoutSagaStatus
{
    Initiated,
    CartSnapshotReceived,
    ProductSnapshotsReceived,
    StockValidated,
    StockDeducted,
    OrderCreated,
    CartCleared,
    Completed,
    Failed
}

/// <summary>
/// CheckoutSaga aggregate root.
/// Orchestrates the checkout process across Cart, ProductCatalog, Inventory, and Order bounded contexts.
/// </summary>
public class CheckoutSaga : AggregateRoot<string>
{
    public string CheckoutId { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public string GuestToken { get; private set; } = string.Empty;
    public string CartId { get; private set; } = string.Empty;
    public CustomerInfoDto CustomerInfo { get; private set; } = null!;
    public ShippingAddressDto ShippingAddress { get; private set; } = null!;

    // Accumulated saga data
    public List<CartItemSnapshotDto> CartItems { get; private set; } = [];
    public List<ProductSnapshotDto> ProductSnapshots { get; private set; } = [];

    // Saga status
    public CheckoutSagaStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime InitiatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Constructor for new saga (called during Initiate factory method).
    /// </summary>
    public CheckoutSaga(string checkoutId) : base(checkoutId)
    {
        CheckoutId = checkoutId;
    }

    /// <summary>
    /// Factory method to initiate a new checkout saga.
    /// </summary>
    public static CheckoutSaga Initiate(
        string checkoutId,
        string orderId,
        string guestToken,
        string cartId,
        CustomerInfoDto customerInfo,
        ShippingAddressDto shippingAddress)
    {
        var saga = new CheckoutSaga(checkoutId);
        saga.AppendEvent(new CheckoutSagaInitiatedEvent
        {
            AggregateId = checkoutId,
            CheckoutId = checkoutId,
            OrderId = orderId,
            GuestToken = guestToken,
            CartId = cartId,
            CustomerInfo = customerInfo,
            ShippingAddress = shippingAddress
        });
        return saga;
    }

    /// <summary>
    /// Handle cart snapshot provided event.
    /// </summary>
    public void HandleCartSnapshotProvided(List<CartItemSnapshotDto> cartItems)
    {
        if (Status != CheckoutSagaStatus.Initiated)
            throw new InvalidOperationException($"Cannot handle cart snapshot in status {Status}");

        AppendEvent(new CartSnapshotReceivedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId,
            CartItems = cartItems
        });
    }

    /// <summary>
    /// Handle product snapshots provided event.
    /// </summary>
    public void HandleProductSnapshotsProvided(List<ProductSnapshotDto> productSnapshots)
    {
        if (Status != CheckoutSagaStatus.CartSnapshotReceived)
            throw new InvalidOperationException($"Cannot handle product snapshots in status {Status}");

        AppendEvent(new ProductSnapshotsReceivedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId,
            ProductSnapshots = productSnapshots
        });
    }

    /// <summary>
    /// Handle stock validation result.
    /// </summary>
    public void HandleStockValidated(bool allAvailable, List<StockValidationResultDto> results)
    {
        if (Status != CheckoutSagaStatus.ProductSnapshotsReceived)
            throw new InvalidOperationException($"Cannot handle stock validation in status {Status}");

        if (!allAvailable)
        {
            Fail("Insufficient stock available", "StockValidation");
            return;
        }

        AppendEvent(new StockValidationCompletedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId,
            AllAvailable = allAvailable,
            Results = results
        });
    }

    /// <summary>
    /// Handle stock deducted event.
    /// </summary>
    public void HandleStockDeducted()
    {
        if (Status != CheckoutSagaStatus.StockValidated)
            throw new InvalidOperationException($"Cannot handle stock deducted in status {Status}");

        AppendEvent(new StockDeductedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId
        });
    }

    /// <summary>
    /// Handle order created event.
    /// </summary>
    public void HandleOrderCreated(string orderNumber)
    {
        if (Status != CheckoutSagaStatus.StockDeducted)
            throw new InvalidOperationException($"Cannot handle order created in status {Status}");

        AppendEvent(new OrderCreatedInSagaEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId,
            OrderNumber = orderNumber
        });
    }

    /// <summary>
    /// Handle cart cleared event.
    /// </summary>
    public void HandleCartCleared()
    {
        if (Status != CheckoutSagaStatus.OrderCreated)
            throw new InvalidOperationException($"Cannot handle cart cleared in status {Status}");

        AppendEvent(new CartClearedInSagaEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId
        });
    }

    /// <summary>
    /// Handle order finalized event.
    /// </summary>
    public void HandleOrderFinalized()
    {
        if (Status != CheckoutSagaStatus.CartCleared)
            throw new InvalidOperationException($"Cannot handle order finalized in status {Status}");

        AppendEvent(new OrderFinalizedInSagaEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId
        });

        AppendEvent(new CheckoutSagaCompletedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId
        });
    }

    /// <summary>
    /// Mark saga as failed.
    /// </summary>
    public void Fail(string reason, string failedAt)
    {
        if (Status == CheckoutSagaStatus.Failed || Status == CheckoutSagaStatus.Completed)
            throw new InvalidOperationException($"Cannot fail saga in status {Status}");

        AppendEvent(new CheckoutSagaFailedEvent
        {
            AggregateId = Id,
            CheckoutId = CheckoutId,
            FailureReason = reason,
            FailedAt = failedAt
        });
    }

    /// <summary>
    /// Apply domain events to update aggregate state.
    /// </summary>
    public override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case CheckoutSagaInitiatedEvent e:
                CheckoutId = e.CheckoutId;
                OrderId = e.OrderId;
                GuestToken = e.GuestToken;
                CartId = e.CartId;
                CustomerInfo = e.CustomerInfo;
                ShippingAddress = e.ShippingAddress;
                Status = CheckoutSagaStatus.Initiated;
                InitiatedAt = DateTime.UtcNow;
                break;

            case CartSnapshotReceivedEvent e:
                CartItems = e.CartItems;
                Status = CheckoutSagaStatus.CartSnapshotReceived;
                break;

            case ProductSnapshotsReceivedEvent e:
                ProductSnapshots = e.ProductSnapshots;
                Status = CheckoutSagaStatus.ProductSnapshotsReceived;
                break;

            case StockValidationCompletedEvent e:
                Status = CheckoutSagaStatus.StockValidated;
                break;

            case StockDeductedEvent:
                Status = CheckoutSagaStatus.StockDeducted;
                break;

            case OrderCreatedInSagaEvent:
                Status = CheckoutSagaStatus.OrderCreated;
                break;

            case CartClearedInSagaEvent:
                Status = CheckoutSagaStatus.CartCleared;
                break;

            case OrderFinalizedInSagaEvent:
                Status = CheckoutSagaStatus.CartCleared;
                break;

            case CheckoutSagaCompletedEvent:
                Status = CheckoutSagaStatus.Completed;
                CompletedAt = DateTime.UtcNow;
                break;

            case CheckoutSagaFailedEvent e:
                Status = CheckoutSagaStatus.Failed;
                FailureReason = e.FailureReason;
                CompletedAt = DateTime.UtcNow;
                break;
        }
    }
}
