using ECommerceMvp.Cart.Application;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.Cart.CommandApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly ILogger<CartController> _logger;

    public CartController(ICommandEnqueuer commandEnqueuer, ILogger<CartController> logger)
    {
        _commandEnqueuer = commandEnqueuer ?? throw new ArgumentNullException(nameof(commandEnqueuer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new shopping cart
    /// POST /api/cart/create
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateCart([FromBody] CreateCartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestToken))
            return BadRequest(new { error = "GuestToken is required" });

        try
        {
            var command = new CreateCartCommand { GuestToken = request.GuestToken };
            await _commandEnqueuer.EnqueueAsync(command);
            _logger.LogInformation("CreateCartCommand enqueued for guest {GuestToken}", request.GuestToken);
            return Accepted(new { status = "accepted", guestToken = request.GuestToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing CreateCartCommand");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add item to cart
    /// POST /api/cart/items
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestToken))
            return BadRequest(new { error = "GuestToken is required" });
        if (string.IsNullOrWhiteSpace(request.ProductId))
            return BadRequest(new { error = "ProductId is required" });
        if (request.Quantity < 1)
            return BadRequest(new { error = "Quantity must be >= 1" });

        try
        {
            var command = new AddCartItemCommand
            {
                GuestToken = request.GuestToken,
                ProductId = request.ProductId,
                Quantity = request.Quantity
            };
            await _commandEnqueuer.EnqueueAsync(command);
            _logger.LogInformation("AddCartItemCommand enqueued for guest {GuestToken}", request.GuestToken);
            return Accepted(new { status = "accepted", guestToken = request.GuestToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing AddCartItemCommand");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update cart item quantity
    /// PUT /api/cart/items/{productId}
    /// </summary>
    [HttpPut("items/{productId}")]
    public async Task<IActionResult> UpdateItemQuantity(string productId, [FromBody] UpdateCartItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GuestToken))
            return BadRequest(new { error = "GuestToken is required" });
        if (request.NewQuantity < 1)
            return BadRequest(new { error = "Quantity must be >= 1" });

        try
        {
            var command = new UpdateCartItemQtyCommand
            {
                GuestToken = request.GuestToken,
                ProductId = productId,
                NewQuantity = request.NewQuantity
            };
            await _commandEnqueuer.EnqueueAsync(command);
            _logger.LogInformation("UpdateCartItemQtyCommand enqueued for guest {GuestToken}", request.GuestToken);
            return Accepted(new { status = "accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing UpdateCartItemQtyCommand");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove item from cart
    /// DELETE /api/cart/items/{productId}
    /// </summary>
    [HttpDelete("items/{productId}")]
    public async Task<IActionResult> RemoveItem(string productId, [FromQuery] string guestToken)
    {
        if (string.IsNullOrWhiteSpace(guestToken))
            return BadRequest(new { error = "GuestToken is required" });

        try
        {
            var command = new RemoveCartItemCommand
            {
                GuestToken = guestToken,
                ProductId = productId
            };
            await _commandEnqueuer.EnqueueAsync(command);
            _logger.LogInformation("RemoveCartItemCommand enqueued for guest {GuestToken}", guestToken);
            return Accepted(new { status = "accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing RemoveCartItemCommand");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clear all items from cart
    /// DELETE /api/cart
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearCart([FromQuery] string guestToken)
    {
        if (string.IsNullOrWhiteSpace(guestToken))
            return BadRequest(new { error = "GuestToken is required" });

        try
        {
            var command = new ClearCartCommand { GuestToken = guestToken };
            await _commandEnqueuer.EnqueueAsync(command);
            _logger.LogInformation("ClearCartCommand enqueued for guest {GuestToken}", guestToken);
            return Accepted(new { status = "accepted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing ClearCartCommand");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class CreateCartRequest
{
    public string GuestToken { get; set; } = null!;
}

public class AddCartItemRequest
{
    public string GuestToken { get; set; } = null!;
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}

public class UpdateCartItemRequest
{
    public string GuestToken { get; set; } = null!;
    public int NewQuantity { get; set; }
}
