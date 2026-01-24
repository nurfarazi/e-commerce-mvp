using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Cart.Domain;

/// <summary>
/// Value Object: CartId (unique identifier for a shopping cart)
/// </summary>
public class CartId : ValueObject
{
    public string Value { get; }

    public CartId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CartId cannot be empty", nameof(value));
        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(CartId cartId) => cartId.Value;
}

/// <summary>
/// Value Object: GuestToken (unique identifier for guest user)
/// </summary>
public class GuestToken : ValueObject
{
    public string Value { get; }

    public GuestToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("GuestToken cannot be empty", nameof(value));
        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(GuestToken token) => token.Value;
}

/// <summary>
/// Value Object: Quantity (must be >= 1)
/// </summary>
public class Quantity : ValueObject
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value < 1)
            throw new ArgumentException("Quantity must be at least 1", nameof(value));
        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
    public static implicit operator int(Quantity qty) => qty.Value;
}

/// <summary>
/// Value Object: ProductId (reference to catalog product)
/// </summary>
public class ProductId : ValueObject
{
    public string Value { get; }

    public ProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProductId cannot be empty", nameof(value));
        Value = value;
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    public static implicit operator string(ProductId productId) => productId.Value;
}

/// <summary>
/// Entity: CartItem (item in shopping cart)
/// </summary>
public class CartItem : IEquatable<CartItem>
{
    public ProductId ProductId { get; }
    public Quantity Quantity { get; set; }

    public CartItem(ProductId productId, Quantity quantity)
    {
        ProductId = productId ?? throw new ArgumentNullException(nameof(productId));
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
    }

    public bool Equals(CartItem? other)
    {
        return other != null && ProductId.Equals(other.ProductId);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CartItem);
    }

    public override int GetHashCode()
    {
        return ProductId.GetHashCode();
    }
}

/// <summary>
/// Aggregate Root: ShoppingCart
/// Manages shopping cart items for guest users with business logic and invariant validation
/// </summary>
public class ShoppingCart : AggregateRoot<CartId>
{
    private readonly List<CartItem> _items = new();

    public CartId CartId { get; private set; }
    public GuestToken GuestToken { get; private set; }
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    // For event sourcing - parameterless constructor
    public ShoppingCart() : base(null!) { }

    private ShoppingCart(CartId cartId, GuestToken guestToken) : base(cartId)
    {
        CartId = cartId;
        GuestToken = guestToken;
    }

    /// <summary>
    /// Factory method: Create a new shopping cart
    /// </summary>
    public static ShoppingCart Create(CartId cartId, GuestToken guestToken)
    {
        var cart = new ShoppingCart(cartId, guestToken);
        cart.ApplyEvent(new CartCreatedEvent
        {
            CartId = cartId,
            GuestToken = guestToken
        });
        return cart;
    }

    /// <summary>
    /// Add item to cart (or increase quantity if already exists)
    /// Invariant: quantity >= 1, product must exist (soft validation via catalog read model)
    /// </summary>
    public void AddItem(ProductId productId, Quantity quantity)
    {
        if (productId == null)
            throw new ArgumentNullException(nameof(productId));
        if (quantity == null)
            throw new ArgumentNullException(nameof(quantity));

        var existingItem = _items.FirstOrDefault(i => i.ProductId.Equals(productId));
        if (existingItem != null)
        {
            var newQuantity = new Quantity(existingItem.Quantity.Value + quantity.Value);
            ChangeQuantity(productId, newQuantity);
        }
        else
        {
            ApplyEvent(new CartItemAddedEvent
            {
                CartId = CartId,
                ProductId = productId,
                Quantity = quantity
            });
        }
    }

    /// <summary>
    /// Increase quantity of existing cart item
    /// </summary>
    public void IncreaseItem(ProductId productId, Quantity deltaQty)
    {
        if (productId == null)
            throw new ArgumentNullException(nameof(productId));
        if (deltaQty == null)
            throw new ArgumentNullException(nameof(deltaQty));

        var existingItem = _items.FirstOrDefault(i => i.ProductId.Equals(productId));
        if (existingItem == null)
            throw new InvalidOperationException($"Product {productId} not in cart");

        var newQuantity = new Quantity(existingItem.Quantity.Value + deltaQty.Value);
        ChangeQuantity(productId, newQuantity);
    }

    /// <summary>
    /// Change quantity of cart item to specific amount
    /// Invariant: new quantity must be >= 1
    /// </summary>
    public void ChangeQuantity(ProductId productId, Quantity newQuantity)
    {
        if (productId == null)
            throw new ArgumentNullException(nameof(productId));
        if (newQuantity == null)
            throw new ArgumentNullException(nameof(newQuantity));

        var existingItem = _items.FirstOrDefault(i => i.ProductId.Equals(productId));
        if (existingItem == null)
            throw new InvalidOperationException($"Product {productId} not in cart");

        var oldQty = existingItem.Quantity;

        ApplyEvent(new CartItemQuantityUpdatedEvent
        {
            CartId = CartId,
            ProductId = productId,
            OldQuantity = oldQty,
            NewQuantity = newQuantity
        });
    }

    /// <summary>
    /// Remove item from cart
    /// </summary>
    public void RemoveItem(ProductId productId)
    {
        if (productId == null)
            throw new ArgumentNullException(nameof(productId));

        var item = _items.FirstOrDefault(i => i.ProductId.Equals(productId));
        if (item == null)
            throw new InvalidOperationException($"Product {productId} not in cart");

        ApplyEvent(new CartItemRemovedEvent
        {
            CartId = CartId,
            ProductId = productId
        });
    }

    /// <summary>
    /// Clear all items from cart
    /// </summary>
    public void Clear(string checkoutId = "")
    {
        if (_items.Count == 0)
            return;

        ApplyEvent(new CartClearedEvent
        {
            CartId = CartId,
            CheckoutId = checkoutId
        });
    }

    // Event handlers for event sourcing
    public override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case CartCreatedEvent e:
                CartId = e.CartId;
                GuestToken = e.GuestToken;
                break;

            case CartItemAddedEvent e:
                var newItem = new CartItem(e.ProductId, e.Quantity);
                _items.Add(newItem);
                break;

            case CartItemQuantityUpdatedEvent e:
                var item = _items.FirstOrDefault(i => i.ProductId.Equals(e.ProductId));
                if (item != null)
                    item.Quantity = e.NewQuantity;
                break;

            case CartItemRemovedEvent e:
                _items.RemoveAll(i => i.ProductId.Equals(e.ProductId));
                break;

            case CartClearedEvent:
                _items.Clear();
                break;
        }
    }
}
