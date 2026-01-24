using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.ProductCatalog.CommandApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ICommandEnqueuer _commandEnqueuer;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ICommandEnqueuer commandEnqueuer,
        ILogger<ProductsController> logger)
    {
        _commandEnqueuer = commandEnqueuer ?? throw new ArgumentNullException(nameof(commandEnqueuer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Create product request: {ProductId}", request.ProductId);

        // Validate request
        if (string.IsNullOrWhiteSpace(request.ProductId))
            return BadRequest(new { error = "ProductId is required" });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        if (request.Price < 0)
            return BadRequest(new { error = "Price cannot be negative" });

        var command = new CreateProductCommand
        {
            ProductId = request.ProductId,
            Name = request.Name,
            Description = request.Description,
            Sku = request.Sku,
            Price = request.Price,
            Currency = request.Currency ?? "USD"
        };

        // Enqueue command to RabbitMQ
        await _commandEnqueuer.EnqueueAsync(command, "productcatalog.commands", cancellationToken);

        return Accepted(new { requestId = Guid.NewGuid(), productId = request.ProductId, status = "accepted" });
    }

    [HttpPut("{productId}/activate")]
    public async Task<IActionResult> ActivateProduct(string productId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activate product request: {ProductId}", productId);

        if (string.IsNullOrWhiteSpace(productId))
            return BadRequest(new { error = "ProductId is required" });

        var command = new ActivateProductCommand { ProductId = productId };

        // Enqueue command to RabbitMQ
        await _commandEnqueuer.EnqueueAsync(command, "productcatalog.commands", cancellationToken);

        return Accepted(new { requestId = Guid.NewGuid(), productId = productId, status = "accepted" });
    }

    public class CreateProductRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Currency { get; set; }
    }
}
