using ECommerceMvp.Inventory.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ECommerceMvp.Inventory.QueryApi.Controllers;

/// <summary>
/// API controller for inventory queries.
/// Handles stock availability and low stock queries.
/// </summary>
[ApiController]
[Route("api/inventory")]
public class StockQueryController : ControllerBase
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<StockQueryController> _logger;
    private readonly string _databaseName;

    public StockQueryController(
        IMongoClient mongoClient,
        ILogger<StockQueryController> logger)
    {
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = "ecommerce";
    }

    /// <summary>
    /// Get stock availability for a specific product.
    /// GET /api/inventory/{productId}/availability
    /// </summary>
    [HttpGet("{productId}/availability")]
    [ProducesResponseType(typeof(StockAvailabilityView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStockAvailability(string productId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying stock availability for product {ProductId}", productId);

            var database = _mongoClient.GetDatabase(_databaseName);
            var collection = database.GetCollection<StockAvailabilityView>("StockAvailability");

            var result = await collection.Find(
                Builders<StockAvailabilityView>.Filter.Eq(s => s.ProductId, productId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result == null)
            {
                _logger.LogWarning("Stock availability not found for product {ProductId}", productId);
                return NotFound(new { error = "Product not found in inventory" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying stock availability for product {ProductId}", productId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get stock availability for multiple products.
    /// POST /api/inventory/availability/batch
    /// </summary>
    [HttpPost("availability/batch")]
    [ProducesResponseType(typeof(List<StockAvailabilityView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMultipleStockAvailability(
        [FromBody] BatchStockQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying stock availability for {ProductCount} products", query.ProductIds.Count);

            if (query.ProductIds == null || query.ProductIds.Count == 0)
                return BadRequest(new { error = "ProductIds cannot be empty" });

            var database = _mongoClient.GetDatabase(_databaseName);
            var collection = database.GetCollection<StockAvailabilityView>("StockAvailability");

            var results = await collection.Find(
                Builders<StockAvailabilityView>.Filter.In(s => s.ProductId, query.ProductIds))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying multiple stock availability");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all products with low stock.
    /// GET /api/inventory/low-stock
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<LowStockView>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLowStockProducts(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying low stock products");

            var database = _mongoClient.GetDatabase(_databaseName);
            var collection = database.GetCollection<LowStockView>("LowStock");

            var results = await collection.Find(
                Builders<LowStockView>.Filter.Eq(l => l.IsLow, true))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying low stock products");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if a specific product is in stock.
    /// GET /api/inventory/{productId}/in-stock
    /// </summary>
    [HttpGet("{productId}/in-stock")]
    [ProducesResponseType(typeof(InStockCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> IsInStock(string productId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking if product {ProductId} is in stock", productId);

            var database = _mongoClient.GetDatabase(_databaseName);
            var collection = database.GetCollection<StockAvailabilityView>("StockAvailability");

            var result = await collection.Find(
                Builders<StockAvailabilityView>.Filter.Eq(s => s.ProductId, productId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result == null)
            {
                _logger.LogWarning("Product {ProductId} not found", productId);
                return NotFound(new { error = "Product not found in inventory" });
            }

            return Ok(new InStockCheckResponse
            {
                ProductId = productId,
                InStock = result.InStockFlag,
                AvailableQuantity = result.AvailableQuantity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stock for product {ProductId}", productId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

#region Request/Response DTOs

public class BatchStockQuery
{
    public List<string> ProductIds { get; set; } = new();
}

public class InStockCheckResponse
{
    public string ProductId { get; set; } = string.Empty;
    public bool InStock { get; set; }
    public int AvailableQuantity { get; set; }
}

#endregion
