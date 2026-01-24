using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Domain;

/// <summary>
/// Value object for product price.
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
/// </summary>
public class Product : AggregateRoot<string>
{
    private Product(string id) : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Sku Sku { get; private set; } = null!;
    public Price Price { get; private set; } = null!;
    public bool IsActive { get; private set; }

    /// <summary>
    /// Create a new product in the catalog.
    /// </summary>
    public static Product Create(string id, string name, string description, Sku sku, Price price)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Product ID cannot be empty", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty", nameof(name));
        if (sku == null)
            throw new ArgumentNullException(nameof(sku));
        if (price == null)
            throw new ArgumentNullException(nameof(price));

        var product = new Product(id);
        product.AppendEvent(new ProductCreatedEvent
        {
            ProductId = id,
            Name = name,
            Description = description,
            Sku = sku.Value,
            Price = price.Amount,
            Currency = price.Currency
        });

        // Apply event to set state
        product.ApplyEvent(product.UncommittedEvents.First());

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
    /// Update product information.
    /// </summary>
    public void Update(string name, string description, Price price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty", nameof(name));
        if (price == null)
            throw new ArgumentNullException(nameof(price));

        if (!IsActive)
            throw new InvalidOperationException("Cannot update an inactive product");

        var @event = new ProductUpdatedEvent
        {
            ProductId = Id,
            Name = name,
            Description = description,
            Price = price.Amount,
            Currency = price.Currency
        };

        AppendEvent(@event);
        ApplyEvent(@event);
    }

    /// <summary>
    /// Activate the product in the catalog.
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
                Name = created.Name;
                Description = created.Description;
                Sku = new Sku(created.Sku);
                Price = new Price(created.Price, created.Currency);
                IsActive = true;
                break;

            case ProductUpdatedEvent updated:
                Name = updated.Name;
                Description = updated.Description;
                Price = new Price(updated.Price, updated.Currency);
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
