using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Cart.Domain;

/// <summary>
/// Domain Event: CartCreated (occurs when a new shopping cart is created)
/// </summary>
public class CartCreatedEvent : DomainEvent
{
    public CartId CartId { get; set; } = null!;
    public GuestToken GuestToken { get; set; } = null!;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain Event: CartItemAdded (occurs when an item is added to cart)
/// </summary>
public class CartItemAddedEvent : DomainEvent
{
    public CartId CartId { get; set; } = null!;
    public ProductId ProductId { get; set; } = null!;
    public Quantity Quantity { get; set; } = null!;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain Event: CartItemQuantityUpdated (occurs when cart item quantity changes)
/// </summary>
public class CartItemQuantityUpdatedEvent : DomainEvent
{
    public CartId CartId { get; set; } = null!;
    public ProductId ProductId { get; set; } = null!;
    public Quantity OldQuantity { get; set; } = null!;
    public Quantity NewQuantity { get; set; } = null!;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain Event: CartItemRemoved (occurs when item is removed from cart)
/// </summary>
public class CartItemRemovedEvent : DomainEvent
{
    public CartId CartId { get; set; } = null!;
    public ProductId ProductId { get; set; } = null!;
    public override int EventVersion => 1;
}

/// <summary>
/// Domain Event: CartCleared (occurs when all items are removed from cart)
/// </summary>
public class CartClearedEvent : DomainEvent
{
    public CartId CartId { get; set; } = null!;
    public override int EventVersion => 1;
}
