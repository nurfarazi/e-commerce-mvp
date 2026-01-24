using ECommerceMvp.Shared.Application;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Application;

#region Read Model DTOs

/// <summary>
/// Product List View projection.
/// CQRS Read Model: ProductListView (productId, name, price, isActive)
/// </summary>
public class ProductListView
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
}

/// <summary>
/// Product Detail View projection.
/// CQRS Read Model: ProductDetailView (sku, name, description, price, isActive)
/// </summary>
public class ProductDetailView
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>
/// Product read model DTO (compatible with existing code).
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

#endregion

#region Queries

/// <summary>
/// Query: Get product detail view by ID.
/// </summary>
public class GetProductByIdQuery : IQuery<ProductDetailView?>
{
    public string ProductId { get; set; } = string.Empty;
}

/// <summary>
/// Query: List all active products (ProductListView).
/// </summary>
public class ListActiveProductsQuery : IQuery<IEnumerable<ProductListView>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Query: Get all products including inactive.
/// </summary>
public class ListAllProductsQuery : IQuery<IEnumerable<ProductListView>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Query: Search products by name.
/// </summary>
public class SearchProductsByNameQuery : IQuery<IEnumerable<ProductListView>>
{
    public string SearchTerm { get; set; } = string.Empty;
    public bool OnlyActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

#endregion

#region Query Handlers

/// <summary>
/// Handler for GetProductByIdQuery.
/// Returns ProductDetailView for the specified product.
/// </summary>
public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDetailView?>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<GetProductByIdQueryHandler> _logger;

    public GetProductByIdQueryHandler(IQueryService queryService, ILogger<GetProductByIdQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProductDetailView?> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying product detail view for {ProductId}", query.ProductId);

            if (string.IsNullOrWhiteSpace(query.ProductId))
            {
                _logger.LogWarning("ProductId is required");
                return null;
            }

            // Implementation would query from the read model database
            // Placeholder: actual implementation in QueryAPI
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", query.ProductId);
            return null;
        }
    }
}

/// <summary>
/// Handler for ListActiveProductsQuery.
/// Returns paginated ProductListView of only active products.
/// </summary>
public class ListActiveProductsQueryHandler : IQueryHandler<ListActiveProductsQuery, IEnumerable<ProductListView>>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<ListActiveProductsQueryHandler> _logger;

    public ListActiveProductsQueryHandler(IQueryService queryService, ILogger<ListActiveProductsQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ProductListView>> HandleAsync(
        ListActiveProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Listing active products: page {Page}, size {PageSize}", query.Page, query.PageSize);

            if (query.Page < 1)
                query.Page = 1;
            if (query.PageSize < 1)
                query.PageSize = 20;
            if (query.PageSize > 100)
                query.PageSize = 100;

            // Implementation would query from the read model database
            // Placeholder: actual implementation in QueryAPI
            return Enumerable.Empty<ProductListView>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active products");
            return Enumerable.Empty<ProductListView>();
        }
    }
}

/// <summary>
/// Handler for ListAllProductsQuery.
/// Returns paginated ProductListView of all products (active and inactive).
/// </summary>
public class ListAllProductsQueryHandler : IQueryHandler<ListAllProductsQuery, IEnumerable<ProductListView>>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<ListAllProductsQueryHandler> _logger;

    public ListAllProductsQueryHandler(IQueryService queryService, ILogger<ListAllProductsQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ProductListView>> HandleAsync(
        ListAllProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Listing all products: page {Page}, size {PageSize}", query.Page, query.PageSize);

            if (query.Page < 1)
                query.Page = 1;
            if (query.PageSize < 1)
                query.PageSize = 20;
            if (query.PageSize > 100)
                query.PageSize = 100;

            // Implementation would query from the read model database
            // Placeholder: actual implementation in QueryAPI
            return Enumerable.Empty<ProductListView>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all products");
            return Enumerable.Empty<ProductListView>();
        }
    }
}

/// <summary>
/// Handler for SearchProductsByNameQuery.
/// Returns paginated ProductListView matching the search term.
/// </summary>
public class SearchProductsByNameQueryHandler : IQueryHandler<SearchProductsByNameQuery, IEnumerable<ProductListView>>
{
    private readonly IQueryService _queryService;
    private readonly ILogger<SearchProductsByNameQueryHandler> _logger;

    public SearchProductsByNameQueryHandler(IQueryService queryService, ILogger<SearchProductsByNameQueryHandler> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ProductListView>> HandleAsync(
        SearchProductsByNameQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Searching products by name: '{SearchTerm}', page {Page}, size {PageSize}",
                query.SearchTerm, query.Page, query.PageSize);

            if (string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                _logger.LogWarning("Search term is required");
                return Enumerable.Empty<ProductListView>();
            }

            if (query.Page < 1)
                query.Page = 1;
            if (query.PageSize < 1)
                query.PageSize = 20;
            if (query.PageSize > 100)
                query.PageSize = 100;

            // Implementation would query from the read model database with text search
            // Placeholder: actual implementation in QueryAPI
            return Enumerable.Empty<ProductListView>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products by name");
            return Enumerable.Empty<ProductListView>();
        }
    }
}

#endregion

