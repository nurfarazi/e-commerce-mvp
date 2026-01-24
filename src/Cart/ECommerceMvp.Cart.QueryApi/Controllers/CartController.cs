using ECommerceMvp.Cart.Application;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.Cart.QueryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly IQueryHandler<GetCartByGuestTokenQuery, CartView?> _queryHandler;
    private readonly ILogger<CartController> _logger;

    public CartController(IQueryHandler<GetCartByGuestTokenQuery, CartView?> queryHandler, ILogger<CartController> logger)
    {
        _queryHandler = queryHandler ?? throw new ArgumentNullException(nameof(queryHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get shopping cart by guest token
    /// GET /api/cart?guestToken={token}
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCart([FromQuery] string guestToken)
    {
        if (string.IsNullOrWhiteSpace(guestToken))
            return BadRequest(new { error = "GuestToken is required" });

        try
        {
            var query = new GetCartByGuestTokenQuery { GuestToken = guestToken };
            var cart = await _queryHandler.Handle(query);

            if (cart == null)
                return NotFound(new { error = "Cart not found" });

            return Ok(cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cart for guest {GuestToken}", guestToken);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
