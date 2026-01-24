using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.Checkout.Application;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.Checkout.CommandApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(ICommandDispatcher commandDispatcher, ILogger<CheckoutController> logger)
    {
        _commandDispatcher = commandDispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Initiate a new checkout saga.
    /// </summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateCheckout([FromBody] InitiateCheckoutRequest request)
    {
        try
        {
            var command = new InitiateCheckoutCommand
            {
                CheckoutId = Guid.NewGuid().ToString(),
                OrderId = request.OrderId,
                GuestToken = request.GuestToken,
                CartId = request.CartId,
                IdempotencyKey = request.IdempotencyKey,
                CustomerInfo = new CustomerInfoDto
                {
                    Name = request.CustomerInfo.Name,
                    Phone = request.CustomerInfo.Phone,
                    Email = request.CustomerInfo.Email
                },
                ShippingAddress = new ShippingAddressDto
                {
                    Line1 = request.ShippingAddress.Line1,
                    Line2 = request.ShippingAddress.Line2,
                    City = request.ShippingAddress.City,
                    PostalCode = request.ShippingAddress.PostalCode,
                    Country = request.ShippingAddress.Country
                }
            };

            var response = await _commandDispatcher.DispatchAsync<InitiateCheckoutCommand, InitiateCheckoutResponse>(command);

            if (response.Success)
            {
                _logger.LogInformation("Checkout initiated: {CheckoutId}", response.CheckoutId);
                return Accepted(new { checkoutId = response.CheckoutId });
            }

            return BadRequest(new { error = response.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating checkout");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class InitiateCheckoutRequest
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public string GuestToken { get; set; } = string.Empty;
    public string CartId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public CustomerInfoRequest CustomerInfo { get; set; } = null!;
    public ShippingAddressRequest ShippingAddress { get; set; } = null!;
}

public class CustomerInfoRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class ShippingAddressRequest
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";
}
