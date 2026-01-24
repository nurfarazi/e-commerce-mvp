using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.ProductCatalog.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Application;

/// <summary>
/// Command: Create a new product.
/// </summary>
public class CreateProductCommand : ICommand<CreateProductResponse>
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
}

public class CreateProductResponse
{
    public string ProductId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for CreateProductCommand.
/// </summary>
public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        IIdempotencyStore idempotencyStore,
        ILogger<CreateProductCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateProductResponse> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating product {ProductId}", command.ProductId);

            // Validate command
            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new CreateProductResponse { Success = false, Error = "ProductId is required" };

            if (string.IsNullOrWhiteSpace(command.Name))
                return new CreateProductResponse { Success = false, Error = "Name is required" };

            if (command.Price < 0)
                return new CreateProductResponse { Success = false, Error = "Price cannot be negative" };

            // Create product aggregate
            var sku = new Sku(command.Sku);
            var price = new Price(command.Price, command.Currency);
            var product = Product.Create(command.ProductId, command.Name, command.Description, sku, price);

            // Save aggregate
            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);

            // Publish events
            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(
                    evt,
                    Guid.NewGuid().ToString(), // CorrelationId
                    null,
                    null,
                    null))
                .ToList();

            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} created successfully", command.ProductId);

            return new CreateProductResponse { ProductId = command.ProductId, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product {ProductId}", command.ProductId);
            return new CreateProductResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: Activate a product.
/// </summary>
public class ActivateProductCommand : ICommand<ActivateProductResponse>
{
    public string ProductId { get; set; } = string.Empty;
}

public class ActivateProductResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for ActivateProductCommand.
/// </summary>
public class ActivateProductCommandHandler : ICommandHandler<ActivateProductCommand, ActivateProductResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ActivateProductCommandHandler> _logger;

    public ActivateProductCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        ILogger<ActivateProductCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActivateProductResponse> HandleAsync(
        ActivateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Activating product {ProductId}", command.ProductId);

            var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            if (product == null)
                return new ActivateProductResponse { Success = false, Error = "Product not found" };

            product.Activate();
            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);

            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(evt, Guid.NewGuid().ToString()))
                .ToList();

            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} activated successfully", command.ProductId);
            return new ActivateProductResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating product {ProductId}", command.ProductId);
            return new ActivateProductResponse { Success = false, Error = ex.Message };
        }
    }
}
