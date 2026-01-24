using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.Shared.Application;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMvp.ProductCatalog.CommandApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ICommandHandler<CreateProductCommand, CreateProductResponse> _createProductHandler;
    private readonly ICommandHandler<ActivateProductCommand, ActivateProductResponse> _activateProductHandler;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        ICommandHandler<CreateProductCommand, CreateProductResponse> createProductHandler,
        ICommandHandler<ActivateProductCommand, ActivateProductResponse> activateProductHandler,
        ILogger<ProductsController> logger)
    {
        _createProductHandler = createProductHandler ?? throw new ArgumentNullException(nameof(createProductHandler));
        _activateProductHandler = activateProductHandler ?? throw new ArgumentNullException(nameof(activateProductHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Create product request: {ProductId}", request.ProductId);

        var command = new CreateProductCommand
        {
            ProductId = request.ProductId,
            Name = request.Name,
            Description = request.Description,
            Sku = request.Sku,
            Price = request.Price,
            Currency = request.Currency ?? "USD"
        };

        var result = await _createProductHandler.HandleAsync(command, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Accepted(new { requestId = Guid.NewGuid(), productId = result.ProductId });
    }

    [HttpPut("{productId}/activate")]
    public async Task<IActionResult> ActivateProduct(string productId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Activate product request: {ProductId}", productId);

        var command = new ActivateProductCommand { ProductId = productId };
        var result = await _activateProductHandler.HandleAsync(command, cancellationToken);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "Product activated" });
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
