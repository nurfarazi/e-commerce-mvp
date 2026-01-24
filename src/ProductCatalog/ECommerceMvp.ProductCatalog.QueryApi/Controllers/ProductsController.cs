using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ECommerceMvp.ProductCatalog.QueryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMongoCollection<ProductReadModel> _productsCollection;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IMongoClient mongoClient, ILogger<ProductsController> logger)
    {
        _logger = logger;
        var database = mongoClient.GetDatabase("ecommerce");
        _productsCollection = database.GetCollection<ProductReadModel>("Products");
    }

    [HttpGet("{productId}")]
    public async Task<IActionResult> GetProduct(string productId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get product: {ProductId}", productId);

        var product = await _productsCollection
            .Find(Builders<ProductReadModel>.Filter.Eq(p => p.ProductId, productId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (product == null)
            return NotFound();

        return Ok(new ProductDto
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Sku = product.Sku,
            Price = product.Price,
            Currency = product.Currency,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            LastModifiedAt = product.LastModifiedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListProducts(
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("List products: page={Page}, pageSize={PageSize}, isActive={IsActive}", page, pageSize, isActive);

        var filter = Builders<ProductReadModel>.Filter.Empty;
        if (isActive.HasValue)
            filter = Builders<ProductReadModel>.Filter.Eq(p => p.IsActive, isActive.Value);

        var total = await _productsCollection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);

        var products = await _productsCollection
            .Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .SortByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = products.Select(p => new ProductDto
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price,
            Currency = p.Currency,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            LastModifiedAt = p.LastModifiedAt
        }).ToList();

        return Ok(new
        {
            data = dtos,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }
}
