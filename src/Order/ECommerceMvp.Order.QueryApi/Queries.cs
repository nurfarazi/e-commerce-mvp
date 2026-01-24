using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Order.QueryApi;

/// <summary>
/// CQRS Read Model: OrderDetailView
/// Used to display order details to customers (by orderId or orderNumber).
/// </summary>
public class OrderDetailView
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string GuestToken { get; set; } = string.Empty;
    public CustomerInfoView CustomerInfo { get; set; } = null!;
    public ShippingAddressView ShippingAddress { get; set; } = null!;
    public List<OrderLineItemView> LineItems { get; set; } = [];
    public OrderTotalsView Totals { get; set; } = null!;
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Pending";
    public string Status { get; set; } = string.Empty;
    public bool StockCommitted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerInfoView
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class ShippingAddressView
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";
}

public class OrderLineItemView
{
    public string LineItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string SkuSnapshot { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderTotalsView
{
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; } = 0;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// CQRS Read Model: AdminOrderListView
/// Used for admin dashboard to list all orders with summary info.
/// </summary>
public class AdminOrderListView
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Query handler to retrieve order details by order ID.
/// </summary>
public class GetOrderDetailQuery : IQuery<OrderDetailView?>
{
    public string OrderId { get; }

    public GetOrderDetailQuery(string orderId)
    {
        OrderId = orderId;
    }
}

public class GetOrderDetailQueryHandler : IQueryHandler<GetOrderDetailQuery, OrderDetailView?>
{
    private readonly IReadModelStore<OrderDetailView> _readModelStore;
    private readonly ILogger<GetOrderDetailQueryHandler> _logger;

    public GetOrderDetailQueryHandler(
        IReadModelStore<OrderDetailView> readModelStore,
        ILogger<GetOrderDetailQueryHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDetailView?> HandleAsync(
        GetOrderDetailQuery query, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying order details for {OrderId}", query.OrderId);
        return await _readModelStore.GetByIdAsync(query.OrderId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Query handler to retrieve order details by order number.
/// </summary>
public class GetOrderDetailByNumberQuery : IQuery<OrderDetailView?>
{
    public string OrderNumber { get; }

    public GetOrderDetailByNumberQuery(string orderNumber)
    {
        OrderNumber = orderNumber;
    }
}

public class GetOrderDetailByNumberQueryHandler : IQueryHandler<GetOrderDetailByNumberQuery, OrderDetailView?>
{
    private readonly IReadModelStore<OrderDetailView> _readModelStore;
    private readonly ILogger<GetOrderDetailByNumberQueryHandler> _logger;

    public GetOrderDetailByNumberQueryHandler(
        IReadModelStore<OrderDetailView> readModelStore,
        ILogger<GetOrderDetailByNumberQueryHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDetailView?> HandleAsync(
        GetOrderDetailByNumberQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying order details for order number {OrderNumber}", query.OrderNumber);
        var allOrders = await _readModelStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return allOrders.FirstOrDefault(o => o.OrderNumber == query.OrderNumber);
    }
}

/// <summary>
/// Query handler to retrieve all orders for admin dashboard.
/// </summary>
public class GetAllOrdersAdminQuery : IQuery<List<AdminOrderListView>>
{
}

public class GetAllOrdersAdminQueryHandler : IQueryHandler<GetAllOrdersAdminQuery, List<AdminOrderListView>>
{
    private readonly IReadModelStore<AdminOrderListView> _readModelStore;
    private readonly ILogger<GetAllOrdersAdminQueryHandler> _logger;

    public GetAllOrdersAdminQueryHandler(
        IReadModelStore<AdminOrderListView> readModelStore,
        ILogger<GetAllOrdersAdminQueryHandler> logger)
    {
        _readModelStore = readModelStore ?? throw new ArgumentNullException(nameof(readModelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AdminOrderListView>> HandleAsync(
        GetAllOrdersAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying all orders for admin");
        var orders = await _readModelStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return orders.ToList();
    }
}

/// <summary>
/// In-memory read model store for OrderDetailView.
/// </summary>
public class InMemoryOrderDetailReadModelStore : IReadModelStore<OrderDetailView>
{
    private readonly Dictionary<string, OrderDetailView> _orders = [];
    private readonly ILogger<InMemoryOrderDetailReadModelStore> _logger;

    public InMemoryOrderDetailReadModelStore(ILogger<InMemoryOrderDetailReadModelStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDetailView?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving order detail view {OrderId}", id);
        
        if (_orders.TryGetValue(id, out var order))
        {
            return await Task.FromResult(order).ConfigureAwait(false);
        }

        return await Task.FromResult<OrderDetailView?>(null).ConfigureAwait(false);
    }

    public async Task<IEnumerable<OrderDetailView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all order detail views");
        return await Task.FromResult(_orders.Values.AsEnumerable()).ConfigureAwait(false);
    }

    public async Task UpsertAsync(string id, OrderDetailView model, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting order detail view {OrderId}", id);
        _orders[id] = model;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting order detail view {OrderId}", id);
        _orders.Remove(id);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// In-memory read model store for AdminOrderListView.
/// </summary>
public class InMemoryAdminOrderListReadModelStore : IReadModelStore<AdminOrderListView>
{
    private readonly Dictionary<string, AdminOrderListView> _orders = [];
    private readonly ILogger<InMemoryAdminOrderListReadModelStore> _logger;

    public InMemoryAdminOrderListReadModelStore(ILogger<InMemoryAdminOrderListReadModelStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminOrderListView?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving admin order list view {OrderId}", id);
        
        if (_orders.TryGetValue(id, out var order))
        {
            return await Task.FromResult(order).ConfigureAwait(false);
        }

        return await Task.FromResult<AdminOrderListView?>(null).ConfigureAwait(false);
    }

    public async Task<IEnumerable<AdminOrderListView>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving all admin order list views");
        return await Task.FromResult(_orders.Values.OrderByDescending(o => o.CreatedAt).AsEnumerable()).ConfigureAwait(false);
    }

    public async Task UpsertAsync(string id, AdminOrderListView model, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting admin order list view {OrderId}", id);
        _orders[id] = model;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting admin order list view {OrderId}", id);
        _orders.Remove(id);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
