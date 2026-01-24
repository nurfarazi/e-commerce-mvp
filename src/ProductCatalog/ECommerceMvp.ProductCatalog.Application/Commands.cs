using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Domain;
using ECommerceMvp.ProductCatalog.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Application;

#region CreateProductCommand

/// <summary>
/// Command: Create a new product.
/// Command: CreateProductCommand { sku, name, price, description? }
/// </summary>
public class CreateProductCommand : ICommand<CreateProductResponse>
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Description { get; set; }
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
            _logger.LogInformation("Creating product {ProductId} with SKU {Sku}", command.ProductId, command.Sku);

            // Validate command
            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new CreateProductResponse { Success = false, Error = "ProductId is required" };

            if (string.IsNullOrWhiteSpace(command.Sku))
                return new CreateProductResponse { Success = false, Error = "SKU is required" };

            if (string.IsNullOrWhiteSpace(command.Name))
                return new CreateProductResponse { Success = false, Error = "Name is required" };

            if (command.Price < 0)
                return new CreateProductResponse { Success = false, Error = "Price cannot be negative" };

            // Create product aggregate
            var product = Product.Create(
                command.ProductId,
                command.Sku,
                command.Name,
                command.Price,
                command.Description);

            // Capture events BEFORE saving (SaveAsync clears them)
            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(
                    evt,
                    Guid.NewGuid().ToString(), // CorrelationId
                    null,
                    null,
                    null))
                .ToList();

            // Save aggregate
            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);

            // Publish events
            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} created successfully", command.ProductId);

            return new CreateProductResponse { ProductId = command.ProductId, Success = true };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid product creation request: {ProductId}", command.ProductId);
            return new CreateProductResponse { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product {ProductId}", command.ProductId);
            return new CreateProductResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region UpdateProductDetailsCommand

/// <summary>
/// Command: Update product details (name and description).
/// Command: UpdateProductDetailsCommand { productId, name, description? }
/// </summary>
public class UpdateProductDetailsCommand : ICommand<UpdateProductDetailsResponse>
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateProductDetailsResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for UpdateProductDetailsCommand.
/// </summary>
public class UpdateProductDetailsCommandHandler : ICommandHandler<UpdateProductDetailsCommand, UpdateProductDetailsResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UpdateProductDetailsCommandHandler> _logger;

    public UpdateProductDetailsCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        ILogger<UpdateProductDetailsCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateProductDetailsResponse> HandleAsync(
        UpdateProductDetailsCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating product details for {ProductId}", command.ProductId);

            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new UpdateProductDetailsResponse { Success = false, Error = "ProductId is required" };

            if (string.IsNullOrWhiteSpace(command.Name))
                return new UpdateProductDetailsResponse { Success = false, Error = "Name is required" };

            var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            if (product == null)
                return new UpdateProductDetailsResponse { Success = false, Error = "Product not found" };

            product.UpdateDetails(command.Name, command.Description);
            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);

            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(evt, Guid.NewGuid().ToString()))
                .ToList();

            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} details updated successfully", command.ProductId);
            return new UpdateProductDetailsResponse { Success = true };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid update request for product {ProductId}", command.ProductId);
            return new UpdateProductDetailsResponse { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId} details", command.ProductId);
            return new UpdateProductDetailsResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region ChangeProductPriceCommand

/// <summary>
/// Command: Change product price.
/// Command: ChangeProductPriceCommand { productId, newPrice }
/// </summary>
public class ChangeProductPriceCommand : ICommand<ChangeProductPriceResponse>
{
    public string ProductId { get; set; } = string.Empty;
    public decimal NewPrice { get; set; }
    public string Currency { get; set; } = "USD";
}

public class ChangeProductPriceResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for ChangeProductPriceCommand.
/// </summary>
public class ChangeProductPriceCommandHandler : ICommandHandler<ChangeProductPriceCommand, ChangeProductPriceResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ChangeProductPriceCommandHandler> _logger;

    public ChangeProductPriceCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        ILogger<ChangeProductPriceCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChangeProductPriceResponse> HandleAsync(
        ChangeProductPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Changing price for product {ProductId} to {NewPrice} {Currency}",
                command.ProductId, command.NewPrice, command.Currency);

            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new ChangeProductPriceResponse { Success = false, Error = "ProductId is required" };

            if (command.NewPrice < 0)
                return new ChangeProductPriceResponse { Success = false, Error = "Price cannot be negative" };

            var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            if (product == null)
                return new ChangeProductPriceResponse { Success = false, Error = "Product not found" };

            product.ChangePrice(command.NewPrice, command.Currency);
            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);

            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(evt, Guid.NewGuid().ToString()))
                .ToList();

            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} price changed successfully", command.ProductId);
            return new ChangeProductPriceResponse { Success = true };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid price change request for product {ProductId}", command.ProductId);
            return new ChangeProductPriceResponse { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing price for product {ProductId}", command.ProductId);
            return new ChangeProductPriceResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region ActivateProductCommand

/// <summary>
/// Command: Activate a product.
/// Command: ActivateProductCommand { productId }
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

            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new ActivateProductResponse { Success = false, Error = "ProductId is required" };

            var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            if (product == null)
                return new ActivateProductResponse { Success = false, Error = "Product not found" };

            product.Activate();

            // Capture events BEFORE saving (SaveAsync clears them)
            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(evt, Guid.NewGuid().ToString()))
                .ToList();

            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);
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

#endregion

#region DeactivateProductCommand

/// <summary>
/// Command: Deactivate a product.
/// Command: DeactivateProductCommand { productId }
/// </summary>
public class DeactivateProductCommand : ICommand<DeactivateProductResponse>
{
    public string ProductId { get; set; } = string.Empty;
}

public class DeactivateProductResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for DeactivateProductCommand.
/// </summary>
public class DeactivateProductCommandHandler : ICommandHandler<DeactivateProductCommand, DeactivateProductResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DeactivateProductCommandHandler> _logger;

    public DeactivateProductCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        ILogger<DeactivateProductCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeactivateProductResponse> HandleAsync(
        DeactivateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deactivating product {ProductId}", command.ProductId);

            if (string.IsNullOrWhiteSpace(command.ProductId))
                return new DeactivateProductResponse { Success = false, Error = "ProductId is required" };

            var product = await _productRepository.GetByIdAsync(command.ProductId, cancellationToken).ConfigureAwait(false);
            if (product == null)
                return new DeactivateProductResponse { Success = false, Error = "Product not found" };

            product.Deactivate();

            // Capture events BEFORE saving (SaveAsync clears them)
            var envelopes = product.UncommittedEvents
                .Select(evt => new DomainEventEnvelope(evt, Guid.NewGuid().ToString()))
                .ToList();

            await _productRepository.SaveAsync(product, cancellationToken).ConfigureAwait(false);
            await _eventPublisher.PublishAsync(envelopes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Product {ProductId} deactivated successfully", command.ProductId);
            return new DeactivateProductResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating product {ProductId}", command.ProductId);
            return new DeactivateProductResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: GetProductSnapshotsCommand (provide product snapshots to checkout saga)
/// </summary>
public class GetProductSnapshotsCommand : ICommand<GetProductSnapshotsResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
    public List<string> ProductIds { get; set; } = [];
}

public class GetProductSnapshotsResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for GetProductSnapshotsCommand
/// Loads products and publishes ProductSnapshotsProvidedEvent
/// </summary>
public class GetProductSnapshotsCommandHandler : ICommandHandler<GetProductSnapshotsCommand, GetProductSnapshotsResponse>
{
    private readonly IRepository<Product, string> _productRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<GetProductSnapshotsCommandHandler> _logger;

    public GetProductSnapshotsCommandHandler(
        IRepository<Product, string> productRepository,
        IEventPublisher eventPublisher,
        ILogger<GetProductSnapshotsCommandHandler> logger)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetProductSnapshotsResponse> HandleAsync(GetProductSnapshotsCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.CheckoutId))
            return new GetProductSnapshotsResponse { Success = false, Error = "CheckoutId is required" };

        if (command.ProductIds == null || command.ProductIds.Count == 0)
            return new GetProductSnapshotsResponse { Success = false, Error = "ProductIds are required" };

        try
        {
            var snapshots = new List<ProductSnapshot>();

            // Load products and create snapshots
            foreach (var productId in command.ProductIds)
            {
                var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
                if (product == null)
                {
                    // Publish failure event
                    await _eventPublisher.PublishAsync(new[]
                    {
                        new DomainEventEnvelope(
                            new ProductSnapshotFailedEvent
                            {
                                AggregateId = command.CheckoutId,
                                CheckoutId = command.CheckoutId,
                                Reason = $"Product {productId} not found"
                            },
                            command.CheckoutId)
                    }, cancellationToken);

                    _logger.LogWarning("Product snapshot failed for CheckoutId {CheckoutId}: Product {ProductId} not found",
                        command.CheckoutId, productId);
                    return new GetProductSnapshotsResponse { Success = false, Error = $"Product {productId} not found" };
                }

                snapshots.Add(new ProductSnapshot
                {
                    ProductId = product.Id,
                    Sku = product.Sku.Value,
                    Name = product.Name.Value,
                    Price = product.Price.Amount,
                    Currency = product.Price.Currency,
                    IsActive = product.IsActive
                });
            }

            // Publish success event
            await _eventPublisher.PublishAsync(new[]
            {
                new DomainEventEnvelope(
                    new ProductSnapshotsProvidedEvent
                    {
                        AggregateId = command.CheckoutId,
                        CheckoutId = command.CheckoutId,
                        ProductSnapshots = snapshots
                    },
                    command.CheckoutId)
            }, cancellationToken);

            _logger.LogInformation("Product snapshots provided for CheckoutId {CheckoutId} with {SnapshotCount} products",
                command.CheckoutId, snapshots.Count);

            return new GetProductSnapshotsResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product snapshots for CheckoutId {CheckoutId}", command.CheckoutId);
            return new GetProductSnapshotsResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

