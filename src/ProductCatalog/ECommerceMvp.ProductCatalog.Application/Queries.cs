using ECommerceMvp.Shared.Application;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Application;

/// <summary>
/// Query: Get product by ID.
/// </summary>
public class GetProductByIdQuery : IQuery<ProductDto?>
{
    public string ProductId { get; set; } = string.Empty;
}

/// <summary>
/// Query: List all active products.
/// </summary>
public class ListActiveProductsQuery : IQuery<IEnumerable<ProductDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Product read model DTO.
/// </summary>
public class ProductDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>
/// Handler for GetProductByIdQuery.
/// </summary>
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<GetProductByIdQueryHandler> _logger;

    public GetProductByIdQueryHandler(IQueryService queryService, ILogger<GetProductByIdQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProductDto?> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Querying product {ProductId}", query.ProductId);
        // Placeholder: actual implementation in QueryAPI
        return null;
    }
}

/// <summary>
/// Handler for ListActiveProductsQuery.
/// </summary>
public class ListActiveProductsQueryHandler : IQueryHandler<ListActiveProductsQuery, IEnumerable<ProductDto>>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<ListActiveProductsQueryHandler> _logger;

    public ListActiveProductsQueryHandler(IQueryService queryService, ILogger<ListActiveProductsQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ProductDto>> HandleAsync(
        ListActiveProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing active products: page {Page}, size {PageSize}", query.Page, query.PageSize);
        // Placeholder: actual implementation in QueryAPI
        return Enumerable.Empty<ProductDto>();
    }
}
