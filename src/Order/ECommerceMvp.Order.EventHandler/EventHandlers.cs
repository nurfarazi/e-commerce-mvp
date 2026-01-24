using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Domain;
using ECommerceMvp.Order.QueryApi;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Order.EventHandler;

/// <summary>
/// Event handler for OrderCreated event - updates OrderDetailView read model.
/// </summary>
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IReadModelStore<OrderDetailView> _readModelStore;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IReadModelStore<OrderDetailView> readModelStore,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling OrderCreated event for order {OrderId}", @event.OrderId);

        var orderDetailView = new OrderDetailView
        {
            OrderId = @event.OrderId,
            OrderNumber = @event.OrderNumber,
            GuestToken = @event.GuestToken,
            CustomerInfo = new CustomerInfoView
            {
                Name = @event.CustomerInfo.Name,
                Phone = @event.CustomerInfo.Phone,
                Email = @event.CustomerInfo.Email
            },
            ShippingAddress = new ShippingAddressView
            {
                Line1 = @event.ShippingAddress.Line1,
                Line2 = @event.ShippingAddress.Line2,
                City = @event.ShippingAddress.City,
                PostalCode = @event.ShippingAddress.PostalCode,
                Country = @event.ShippingAddress.Country
            },
            LineItems = @event.LineItems.Select(li => new OrderLineItemView
            {
                LineItemId = li.LineItemId,
                ProductId = li.ProductId,
                SkuSnapshot = li.SkuSnapshot,
                NameSnapshot = li.NameSnapshot,
                UnitPriceSnapshot = li.UnitPriceSnapshot,
                Quantity = li.Quantity,
                LineTotal = li.LineTotal
            }).ToList(),
            Totals = new OrderTotalsView
            {
                Subtotal = @event.Totals.Subtotal,
                ShippingFee = @event.Totals.ShippingFee,
                Total = @event.Totals.Total,
                Currency = @event.Totals.Currency
            },
            PaymentMethod = @event.PaymentMethod,
            PaymentStatus = @event.PaymentStatus,
            Status = "Created",
            StockCommitted = false,
            CreatedAt = @event.CreatedAt
        };

        await _readModelStore.UpsertAsync(@event.OrderId, orderDetailView, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("OrderDetailView created for order {OrderId}", @event.OrderId);
    }
}

/// <summary>
/// Event handler for OrderStockCommitted event - updates order status in read model.
/// </summary>
public class OrderStockCommittedEventHandler : IEventHandler<OrderStockCommittedEvent>
{
    private readonly IReadModelStore<OrderDetailView> _readModelStore;
    private readonly ILogger<OrderStockCommittedEventHandler> _logger;

    public OrderStockCommittedEventHandler(
        IReadModelStore<OrderDetailView> readModelStore,
        ILogger<OrderStockCommittedEventHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(OrderStockCommittedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling OrderStockCommitted event for order {OrderId}", @event.OrderId);

        var view = await _readModelStore.GetByIdAsync(@event.OrderId, cancellationToken).ConfigureAwait(false);
        if (view != null)
        {
            view.StockCommitted = true;
            view.Status = "StockCommitted";
            await _readModelStore.UpsertAsync(@event.OrderId, view, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Order {OrderId} stock committed in read model", @event.OrderId);
        }
    }
}

/// <summary>
/// Event handler for OrderCreated event - updates AdminOrderListView read model.
/// </summary>
public class OrderCreatedAdminListEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IReadModelStore<AdminOrderListView> _readModelStore;
    private readonly ILogger<OrderCreatedAdminListEventHandler> _logger;

    public OrderCreatedAdminListEventHandler(
        IReadModelStore<AdminOrderListView> readModelStore,
        ILogger<OrderCreatedAdminListEventHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling OrderCreated event for admin list for order {OrderId}", @event.OrderId);

        var adminView = new AdminOrderListView
        {
            OrderId = @event.OrderId,
            OrderNumber = @event.OrderNumber,
            CustomerName = @event.CustomerInfo.Name,
            CustomerPhone = @event.CustomerInfo.Phone,
            Total = @event.Totals.Total,
            Currency = @event.Totals.Currency,
            CreatedAt = @event.CreatedAt,
            Status = "Created"
        };

        await _readModelStore.UpsertAsync(@event.OrderId, adminView, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("AdminOrderListView created for order {OrderId}", @event.OrderId);
    }
}

/// <summary>
/// Event handler for OrderFinalizedEvent - updates status in read models.
/// </summary>
public class OrderFinalizedEventHandler : IEventHandler<OrderFinalizedEvent>
{
    private readonly IReadModelStore<OrderDetailView> _detailReadModelStore;
    private readonly IReadModelStore<AdminOrderListView> _adminReadModelStore;
    private readonly ILogger<OrderFinalizedEventHandler> _logger;

    public OrderFinalizedEventHandler(
        IReadModelStore<OrderDetailView> detailReadModelStore,
        IReadModelStore<AdminOrderListView> adminReadModelStore,
        ILogger<OrderFinalizedEventHandler> logger)
    {
        _detailReadModelStore = detailReadModelStore ?? throw new ArgumentNullException(nameof(detailReadModelStore));
        _adminReadModelStore = adminReadModelStore ?? throw new ArgumentNullException(nameof(adminReadModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(OrderFinalizedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling OrderFinalized event for order {OrderId}", @event.OrderId);

        var detailView = await _detailReadModelStore.GetByIdAsync(@event.OrderId, cancellationToken).ConfigureAwait(false);
        if (detailView != null)
        {
            detailView.Status = "Finalized";
            await _detailReadModelStore.UpsertAsync(@event.OrderId, detailView, cancellationToken).ConfigureAwait(false);
        }

        var adminView = await _adminReadModelStore.GetByIdAsync(@event.OrderId, cancellationToken).ConfigureAwait(false);
        if (adminView != null)
        {
            adminView.Status = "Finalized";
            await _adminReadModelStore.UpsertAsync(@event.OrderId, adminView, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Order {OrderId} marked as finalized in read models", @event.OrderId);
    }
}

/// <summary>
/// Event handler for StockCommitRequestedIntegrationEvent - publishes to Inventory context.
/// This would typically be a listener that re-publishes to a message broker.
/// </summary>
public class StockCommitRequestedIntegrationEventHandler : IEventHandler<StockCommitRequestedIntegrationEvent>
{
    private readonly ILogger<StockCommitRequestedIntegrationEventHandler> _logger;

    public StockCommitRequestedIntegrationEventHandler(
        ILogger<StockCommitRequestedIntegrationEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(StockCommitRequestedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stock commit requested for order {OrderId} with {ItemCount} items", 
            @event.OrderId, @event.Items.Count);

        // In a real implementation, this would publish to a message broker
        // For MVP, we just log the intent
        foreach (var item in @event.Items)
        {
            _logger.LogInformation("  - Product {ProductId}: {Quantity} units", item.ProductId, item.Quantity);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// Event handler for CartClearRequestedIntegrationEvent - publishes to Cart context.
/// </summary>
public class CartClearRequestedIntegrationEventHandler : IEventHandler<CartClearRequestedIntegrationEvent>
{
    private readonly ILogger<CartClearRequestedIntegrationEventHandler> _logger;

    public CartClearRequestedIntegrationEventHandler(
        ILogger<CartClearRequestedIntegrationEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(CartClearRequestedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cart clear requested for guest {GuestToken}, cart {CartId}, order {OrderId}",
            @event.GuestToken, @event.CartId, @event.OrderId);

        // In a real implementation, this would publish to a message broker
        // For MVP, we just log the intent
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
