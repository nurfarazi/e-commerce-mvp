using Microsoft.AspNetCore.Mvc;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Order.QueryApi;

namespace ECommerceMvp.Order.QueryApiServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderQueriesController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly ILogger<OrderQueriesController> _logger;

    public OrderQueriesController(IQueryBus queryBus, ILogger<OrderQueriesController> logger)
    {
        _queryBus = queryBus ?? throw new ArgumentNullException(nameof(queryBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get order details by order ID.
    /// </summary>
    [HttpGet("{orderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailView>> GetOrderDetail(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting order details for {OrderId}", orderId);

        var query = new GetOrderDetailQuery(orderId);
        var result = await _queryBus.SendAsync<GetOrderDetailQuery, OrderDetailView?>(
            query,
            cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get order details by order number.
    /// </summary>
    [HttpGet("by-number/{orderNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailView>> GetOrderDetailByNumber(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting order details for order number {OrderNumber}", orderNumber);

        var query = new GetOrderDetailByNumberQuery(orderNumber);
        var result = await _queryBus.SendAsync<GetOrderDetailByNumberQuery, OrderDetailView?>(
            query,
            cancellationToken).ConfigureAwait(false);

        if (result == null)
        {
            _logger.LogWarning("Order {OrderNumber} not found", orderNumber);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all orders for admin dashboard.
    /// </summary>
    [HttpGet("admin/orders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AdminOrderListView>>> GetAllOrdersAdmin(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all orders for admin");

        var query = new GetAllOrdersAdminQuery();
        var result = await _queryBus.SendAsync<GetAllOrdersAdminQuery, List<AdminOrderListView>>(
            query,
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}
