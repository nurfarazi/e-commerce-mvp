using ECommerceMvp.Inventory.Application;
using ECommerceMvp.Shared.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Inventory.CommandApi.Controllers;

/// <summary>
/// API controller for inventory commands.
/// Handles: SetStock, ValidateStock, DeductStockForOrder
/// </summary>
[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        ICommandDispatcher commandDispatcher,
        ILogger<InventoryController> logger)
    {
        _commandDispatcher = commandDispatcher ?? throw new ArgumentNullException(nameof(commandDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Set stock quantity for a product (admin operation).
    /// POST /api/inventory/{productId}/set-stock
    /// </summary>
    [HttpPost("{productId}/set-stock")]
    [ProducesResponseType(typeof(SetStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetStock(
        string productId,
        [FromBody] SetStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("SetStock request for product {ProductId} to quantity {NewQuantity}",
                productId, request.NewQuantity);

            var command = new SetStockCommand
            {
                ProductId = productId,
                NewQuantity = request.NewQuantity,
                Reason = request.Reason,
                ChangedBy = request.ChangedBy
            };

            var response = await _commandDispatcher.DispatchAsync<SetStockCommand, SetStockResponse>(command, cancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogWarning("SetStock failed for product {ProductId}: {Error}", productId, response.Error);
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SetStock request for product {ProductId}", productId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validate stock availability for multiple products.
    /// POST /api/inventory/validate-stock
    /// </summary>
    [HttpPost("validate-stock")]
    [ProducesResponseType(typeof(ValidateStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateStock(
        [FromBody] ValidateStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("ValidateStock request for {ItemCount} items", request.Items.Count);
            var command = new ValidateStockCommand
            {
                Items = request.Items.Select(i => new StockValidationItem
                {
                    ProductId = i.ProductId,
                    RequestedQuantity = i.RequestedQuantity
                }).ToList()
            };

            var response = await _commandDispatcher.DispatchAsync<ValidateStockCommand, ValidateStockResponse>(command, cancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogWarning("ValidateStock failed: {Error}", response.Error);
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ValidateStock request");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deduct stock for an order (atomic and idempotent per order).
    /// POST /api/inventory/deduct-for-order
    /// </summary>
    [HttpPost("deduct-for-order")]
    [ProducesResponseType(typeof(DeductStockForOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeductForOrder(
        [FromBody] DeductStockForOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("DeductForOrder request for order {OrderId} with {ItemCount} items",
                request.OrderId, request.Items.Count);

            var command = new DeductStockForOrderCommand
            {
                OrderId = request.OrderId,
                Items = request.Items.Select(i => new StockDeductionItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };

            var response = await _commandDispatcher.DispatchAsync<DeductStockForOrderCommand, DeductStockForOrderResponse>(command, cancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogWarning("DeductForOrder failed for order {OrderId}: {Error}", request.OrderId, response.Error);
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing DeductForOrder request for order {OrderId}", request.OrderId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

#region Request/Response DTOs

public class SetStockRequest
{
    public int NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}

public class ValidateStockRequest
{
    public List<ValidateStockItem> Items { get; set; } = new();
}

public class ValidateStockItem
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
}

public class DeductStockForOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public List<DeductStockItem> Items { get; set; } = new();
}

public class DeductStockItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

#endregion
