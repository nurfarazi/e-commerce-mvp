using Microsoft.AspNetCore.Mvc;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Order.Application;

namespace ECommerceMvp.Order.CommandApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ICommandBus commandBus, ILogger<OrdersController> logger)
    {
        _commandBus = commandBus ?? throw new ArgumentNullException(nameof(commandBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Place an order from a shopping cart.
    /// </summary>
    [HttpPost("place-order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlaceOrderResponse>> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest("Request body is required");

        _logger.LogInformation("Received place-order request for guest {GuestToken}", request.GuestToken);

        var command = new PlaceOrderCommand
        {
            OrderId = Guid.NewGuid().ToString(),
            GuestToken = request.GuestToken,
            CartId = request.CartId,
            IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString(),
            CustomerInfo = request.CustomerInfo,
            ShippingAddress = request.ShippingAddress,
            CartItems = request.CartItems,
            ProductSnapshots = request.ProductSnapshots
        };

        var result = await _commandBus.SendAsync<PlaceOrderCommand, PlaceOrderResponse>(
            command,
            cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Order placed successfully: {OrderId}", result.OrderId);
            return Ok(result);
        }

        _logger.LogWarning("Order placement failed: {Error}", result.Error);
        return BadRequest(result);
    }
}

public class PlaceOrderRequest
{
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public CustomerInfoRequest CustomerInfo { get; set; } = null!;
    public ShippingAddressRequest ShippingAddress { get; set; } = null!;
    public List<CartItemSnapshot> CartItems { get; set; } = [];
    public List<ProductSnapshot> ProductSnapshots { get; set; } = [];
}
