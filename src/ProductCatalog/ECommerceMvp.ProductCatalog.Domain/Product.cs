using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Domain;

/// <summary>
/// Value object for product ID (unique identifier).
/// </summary>
public class ProductId : ValueObject
{
    public string Value { get; }

    public ProductId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product ID cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProductId productId) => productId.Value;
    public static implicit operator ProductId(string value) => new(value);
}

/// <summary>
/// Value object for product name.
/// Constraints: Required, minimum length 2.
/// </summary>
public class ProductName : ValueObject
{
    private const int MinLength = 2;

    public string Value { get; }

    public ProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product name cannot be empty", nameof(value));
        if (value.Length < MinLength)
            throw new ArgumentException($"Product name must be at least {MinLength} characters", nameof(value));

        Value = value.Trim();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProductName name) => name.Value;
    public static implicit operator ProductName(string value) => new(value);
}

/// <summary>
/// Value object for product description.
/// </summary>
public class ProductDescription : ValueObject
{
    public string Value { get; }

    public ProductDescription(string value)
    {
        Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProductDescription description) => description.Value;
    public static implicit operator ProductDescription(string value) => new(value);
}

/// <summary>
/// Value object for product price.
/// Constraints: Price >= 0.
/// </summary>
public class Price : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Price(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Price amount cannot be negative", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}

/// <summary>
/// Value object for product SKU (Stock Keeping Unit).
/// Constraints: Unique and immutable after creation.
/// </summary>
public class Sku : ValueObject
{
    public string Value { get; }

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty", nameof(value));

        Value = value.ToUpperInvariant();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}

/// <summary>
/// Product aggregate root in the ProductCatalog bounded context.
/// 
/// Invariants:
/// - SKU is unique and immutable after creation
/// - Price >= 0
/// - Name required, min length 2
/// - Only Active products can be sold/ordered
/// </summary>
public class Product : AggregateRoot<string>
{
    private Product(string id) : base(id)
    {
    }

    public ProductName Name { get; private set; } = null!;
    public ProductDescription Description { get; private set; } = null!;
    public Sku Sku { get; private set; } = null!;
    public Price Price { get; private set; } = null!;
    public bool IsActive { get; private set; }

    /// <summary>
    /// Create a new product in the catalog.
    /// Behavior: CreateProduct(sku, name, price, description)
    /// </summary>
    public static Product Create(string id, string sku, string name, decimal price, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Product ID cannot be empty", nameof(id));

        var skuVO = new Sku(sku);
        var nameVO = new ProductName(name);
        var priceVO = new Price(price);
        var descriptionVO = new ProductDescription(description ?? string.Empty);

        var product = new Product(id);
        var @event = new ProductCreatedEvent
        {
            ProductId = id,
            Sku = skuVO.Value,
            Name = nameVO.Value,
            Price = priceVO.Amount,
            Description = descriptionVO.Value,
            Currency = priceVO.Currency
        };

        product.AppendEvent(@event);
        product.ApplyEvent(@event);

        return product;
    }

    /// <summary>
    /// Reconstruct a product from its event history (for event sourcing).
    /// </summary>
    public static Product FromHistory(string id)
    {
        return new Product(id);
    }

    /// <summary>
    /// Update product details (name and description).
    /// Behavior: UpdateDetails(name, description)
    /// </summary>
    public void UpdateDetails(string name, string? description = null)
    {
        var newName = new ProductName(name);
        var newDescription = new ProductDescription(description ?? string.Empty);

        if (!IsActive)
            throw new InvalidOperationException("Cannot update an inactive product");

        var @event = new ProductDetailsUpdatedEvent
        {
            ProductId = Id,
            Name = newName.Value,
            Description = newDescription.Value
        };

        AppendEvent(@event);
        ApplyEvent(@event);
    }

    /// <summary>
    /// Change the product price.
    /// Behavior: ChangePrice(newPrice)
    /// </summary>
    public void ChangePrice(decimal newPrice, string currency = "USD")
    {
        var newPriceVO = new Price(newPrice, currency);

        if (!IsActive)
            throw new InvalidOperationException("Cannot change price of an inactive product");

        var @event = new ProductPriceChangedEvent
        {
            ProductId = Id,
            OldPrice = Price.Amount,
            NewPrice = newPriceVO.Amount,
            OldCurrency = Price.Currency,
            NewCurrency = newPriceVO.Currency
        };

        AppendEvent(@event);
        ApplyEvent(@event);
    }

    /// <summary>
    /// Activate the product in the catalog.
    /// Behavior: Activate()
    /// </summary>
    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Product is already active");

        var @event = new ProductActivatedEvent { ProductId = Id };
        AppendEvent(@event);
        ApplyEvent(@event);
    }

    /// <summary>
    /// Deactivate the product from the catalog.
    /// Behavior: Deactivate()
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Product is already inactive");

        var @event = new ProductDeactivatedEvent { ProductId = Id };
        AppendEvent(@event);
        ApplyEvent(@event);
    }

    public override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case ProductCreatedEvent created:
                Name = new ProductName(created.Name);
                Description = new ProductDescription(created.Description);
                Sku = new Sku(created.Sku);
                Price = new Price(created.Price, created.Currency);
                IsActive = true;
                break;

            case ProductDetailsUpdatedEvent updated:
                Name = new ProductName(updated.Name);
                Description = new ProductDescription(updated.Description);
                break;

            case ProductPriceChangedEvent priceChanged:
                Price = new Price(priceChanged.NewPrice, priceChanged.NewCurrency);
                break;

            case ProductActivatedEvent:
                IsActive = true;
                break;

            case ProductDeactivatedEvent:
                IsActive = false;
                break;
        }
    }
}

/// <summary>
/// Exception thrown when product business rule is violated.
/// </summary>
public class ProductDomainException : DomainException
{
    public ProductDomainException(string message) : base(message) { }
}
