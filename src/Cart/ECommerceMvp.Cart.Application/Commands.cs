using ECommerceMvp.Shared.Application;
using ECommerceMvp.Shared.Infrastructure;
using ECommerceMvp.Cart.Domain;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.Cart.Application;

#region Commands

/// <summary>
/// Command: CreateCartCommand (creates new shopping cart for guest)
/// Note: Optional in MVP - typically auto-created on first item add
/// </summary>
public class CreateCartCommand : ICommand<CreateCartResponse>
{
    public string GuestToken { get; set; } = null!;
}

public class CreateCartResponse
{
    public string CartId { get; set; } = null!;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Command Handler: CreateCartCommand
/// </summary>
public class CreateCartCommandHandler : ICommandHandler<CreateCartCommand, CreateCartResponse>
{
    private readonly IRepository<ShoppingCart, CartId> _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<CreateCartCommandHandler> _logger;

    public CreateCartCommandHandler(
        IRepository<ShoppingCart, CartId> repository,
        IEventPublisher eventPublisher,
        IIdempotencyStore idempotencyStore,
        ILogger<CreateCartCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateCartResponse> Handle(CreateCartCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return new CreateCartResponse { Success = false, Error = "GuestToken is required" };

        try
        {
            var cartId = new CartId(Guid.NewGuid().ToString());
            var guestToken = new GuestToken(command.GuestToken);

            var cart = ShoppingCart.Create(cartId, guestToken);
            await _repository.SaveAsync(cart);
            await _eventPublisher.PublishAsync(cart.GetUncommittedEvents());

            _logger.LogInformation("Cart created: {CartId} for guest {GuestToken}", cartId, guestToken);

            return new CreateCartResponse { CartId = cartId, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cart for guest {GuestToken}", command.GuestToken);
            return new CreateCartResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: AddCartItemCommand (add product to cart)
/// </summary>
public class AddCartItemCommand : ICommand<AddCartItemResponse>
{
    public string GuestToken { get; set; } = null!;
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}

public class AddCartItemResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? CartId { get; set; }
}

/// <summary>
/// Command Handler: AddCartItemCommand
/// Validates product exists (soft check) and adds item to cart
/// </summary>
public class AddCartItemCommandHandler : ICommandHandler<AddCartItemCommand, AddCartItemResponse>
{
    private readonly IRepository<ShoppingCart, CartId> _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMongoDatabase _database;
    private readonly ILogger<AddCartItemCommandHandler> _logger;

    public AddCartItemCommandHandler(
        IRepository<ShoppingCart, CartId> repository,
        IEventPublisher eventPublisher,
        IMongoClient mongoClient,
        ILogger<AddCartItemCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _database = mongoClient?.GetDatabase("ecommerce") ?? throw new ArgumentNullException(nameof(mongoClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AddCartItemResponse> Handle(AddCartItemCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return new AddCartItemResponse { Success = false, Error = "GuestToken is required" };
        if (string.IsNullOrWhiteSpace(command.ProductId))
            return new AddCartItemResponse { Success = false, Error = "ProductId is required" };
        if (command.Quantity < 1)
            return new AddCartItemResponse { Success = false, Error = "Quantity must be >= 1" };

        try
        {
            // Soft validation: check product exists in catalog read model
            var productsCollection = _database.GetCollection<dynamic>("Products");
            var productExists = await productsCollection.Find(p => p["productId"] == command.ProductId).AnyAsync();
            if (!productExists)
                return new AddCartItemResponse { Success = false, Error = $"Product {command.ProductId} not found" };

            var guestToken = new GuestToken(command.GuestToken);
            var productId = new ProductId(command.ProductId);
            var quantity = new Quantity(command.Quantity);

            // Try to load existing cart, or create new one
            var cartId = new CartId($"cart-{command.GuestToken}");
            var cart = await _repository.GetByIdAsync(cartId);
            if (cart == null)
            {
                cart = ShoppingCart.Create(cartId, guestToken);
            }

            cart.AddItem(productId, quantity);
            await _repository.SaveAsync(cart);
            await _eventPublisher.PublishAsync(cart.GetUncommittedEvents());

            _logger.LogInformation("Item added to cart {CartId}: {ProductId} x {Quantity}", cartId, productId, quantity);

            return new AddCartItemResponse { Success = true, CartId = cartId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to cart for guest {GuestToken}", command.GuestToken);
            return new AddCartItemResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: UpdateCartItemQtyCommand (change quantity of existing cart item)
/// </summary>
public class UpdateCartItemQtyCommand : ICommand<UpdateCartItemQtyResponse>
{
    public string GuestToken { get; set; } = null!;
    public string ProductId { get; set; } = null!;
    public int NewQuantity { get; set; }
}

public class UpdateCartItemQtyResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Command Handler: UpdateCartItemQtyCommand
/// </summary>
public class UpdateCartItemQtyCommandHandler : ICommandHandler<UpdateCartItemQtyCommand, UpdateCartItemQtyResponse>
{
    private readonly IRepository<ShoppingCart, CartId> _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<UpdateCartItemQtyCommandHandler> _logger;

    public UpdateCartItemQtyCommandHandler(
        IRepository<ShoppingCart, CartId> repository,
        IEventPublisher eventPublisher,
        ILogger<UpdateCartItemQtyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateCartItemQtyResponse> Handle(UpdateCartItemQtyCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return new UpdateCartItemQtyResponse { Success = false, Error = "GuestToken is required" };
        if (string.IsNullOrWhiteSpace(command.ProductId))
            return new UpdateCartItemQtyResponse { Success = false, Error = "ProductId is required" };
        if (command.NewQuantity < 1)
            return new UpdateCartItemQtyResponse { Success = false, Error = "Quantity must be >= 1" };

        try
        {
            var cartId = new CartId($"cart-{command.GuestToken}");
            var cart = await _repository.GetByIdAsync(cartId);
            if (cart == null)
                return new UpdateCartItemQtyResponse { Success = false, Error = "Cart not found" };

            var productId = new ProductId(command.ProductId);
            var newQuantity = new Quantity(command.NewQuantity);

            cart.ChangeQuantity(productId, newQuantity);
            await _repository.SaveAsync(cart);
            await _eventPublisher.PublishAsync(cart.GetUncommittedEvents());

            _logger.LogInformation("Cart {CartId} item {ProductId} quantity updated to {Quantity}", cartId, productId, newQuantity);

            return new UpdateCartItemQtyResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cart item for guest {GuestToken}", command.GuestToken);
            return new UpdateCartItemQtyResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: RemoveCartItemCommand (remove product from cart)
/// </summary>
public class RemoveCartItemCommand : ICommand<RemoveCartItemResponse>
{
    public string GuestToken { get; set; } = null!;
    public string ProductId { get; set; } = null!;
}

public class RemoveCartItemResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Command Handler: RemoveCartItemCommand
/// </summary>
public class RemoveCartItemCommandHandler : ICommandHandler<RemoveCartItemCommand, RemoveCartItemResponse>
{
    private readonly IRepository<ShoppingCart, CartId> _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<RemoveCartItemCommandHandler> _logger;

    public RemoveCartItemCommandHandler(
        IRepository<ShoppingCart, CartId> repository,
        IEventPublisher eventPublisher,
        ILogger<RemoveCartItemCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RemoveCartItemResponse> Handle(RemoveCartItemCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return new RemoveCartItemResponse { Success = false, Error = "GuestToken is required" };
        if (string.IsNullOrWhiteSpace(command.ProductId))
            return new RemoveCartItemResponse { Success = false, Error = "ProductId is required" };

        try
        {
            var cartId = new CartId($"cart-{command.GuestToken}");
            var cart = await _repository.GetByIdAsync(cartId);
            if (cart == null)
                return new RemoveCartItemResponse { Success = false, Error = "Cart not found" };

            var productId = new ProductId(command.ProductId);
            cart.RemoveItem(productId);
            await _repository.SaveAsync(cart);
            await _eventPublisher.PublishAsync(cart.GetUncommittedEvents());

            _logger.LogInformation("Item {ProductId} removed from cart {CartId}", productId, cartId);

            return new RemoveCartItemResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from cart for guest {GuestToken}", command.GuestToken);
            return new RemoveCartItemResponse { Success = false, Error = ex.Message };
        }
    }
}

/// <summary>
/// Command: ClearCartCommand (remove all items from cart)
/// </summary>
public class ClearCartCommand : ICommand<ClearCartResponse>
{
    public string GuestToken { get; set; } = null!;
}

public class ClearCartResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Command Handler: ClearCartCommand
/// </summary>
public class ClearCartCommandHandler : ICommandHandler<ClearCartCommand, ClearCartResponse>
{
    private readonly IRepository<ShoppingCart, CartId> _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ClearCartCommandHandler> _logger;

    public ClearCartCommandHandler(
        IRepository<ShoppingCart, CartId> repository,
        IEventPublisher eventPublisher,
        ILogger<ClearCartCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ClearCartResponse> Handle(ClearCartCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.GuestToken))
            return new ClearCartResponse { Success = false, Error = "GuestToken is required" };

        try
        {
            var cartId = new CartId($"cart-{command.GuestToken}");
            var cart = await _repository.GetByIdAsync(cartId);
            if (cart == null)
                return new ClearCartResponse { Success = false, Error = "Cart not found" };

            cart.Clear();
            await _repository.SaveAsync(cart);
            await _eventPublisher.PublishAsync(cart.GetUncommittedEvents());

            _logger.LogInformation("Cart {CartId} cleared", cartId);

            return new ClearCartResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cart for guest {GuestToken}", command.GuestToken);
            return new ClearCartResponse { Success = false, Error = ex.Message };
        }
    }
}

#endregion

#region Queries

/// <summary>
/// Query: GetCartByGuestTokenQuery (retrieve cart read model)
/// </summary>
public class GetCartByGuestTokenQuery : IQuery<CartView?>
{
    public string GuestToken { get; set; } = null!;
}

/// <summary>
/// Query Handler: GetCartByGuestTokenQuery
/// </summary>
public class GetCartByGuestTokenQueryHandler : IQueryHandler<GetCartByGuestTokenQuery, CartView?>
{
    private readonly IMongoDatabase _database;

    public GetCartByGuestTokenQueryHandler(IMongoClient mongoClient)
    {
        _database = mongoClient?.GetDatabase("ecommerce") ?? throw new ArgumentNullException(nameof(mongoClient));
    }

    public async Task<CartView?> Handle(GetCartByGuestTokenQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.GuestToken))
            return null;

        var cartsCollection = _database.GetCollection<CartReadModel>("Carts");
        var cart = await cartsCollection
            .Find(c => c.GuestToken == query.GuestToken)
            .FirstOrDefaultAsync();

        return cart;
    }
}

#endregion

#region Read Models

/// <summary>
/// CQRS Read Model: CartView (optimized for query performance)
/// </summary>
public class CartView
{
    public string CartId { get; set; } = null!;
    public string GuestToken { get; set; } = null!;
    public List<CartItemView> Items { get; set; } = new();
}

public class CartItemView
{
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}

/// <summary>
/// MongoDB Document: CartReadModel (persisted read model)
/// </summary>
public class CartReadModel : CartView
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Event Handlers

/// <summary>
/// Interface: ICartProjectionWriter (handles domain events for read model projection)
/// </summary>
public interface ICartProjectionWriter
{
    Task HandleCartCreatedAsync(CartCreatedEvent @event);
    Task HandleCartItemAddedAsync(CartItemAddedEvent @event);
    Task HandleCartItemQuantityUpdatedAsync(CartItemQuantityUpdatedEvent @event);
    Task HandleCartItemRemovedAsync(CartItemRemovedEvent @event);
    Task HandleCartClearedAsync(CartClearedEvent @event);
}

#endregion
