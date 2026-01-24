using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Domain;

/// <summary>
/// Domain event: Product was created in the catalog.
/// Event: ProductCreated { productId, sku, name, price, description? }
/// </summary>
public class ProductCreatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Description { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product details were updated.
/// Event: ProductDetailsUpdated { productId, name, description? }
/// </summary>
public class ProductDetailsUpdatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product price was changed.
/// Event: ProductPriceChanged { productId, oldPrice, newPrice }
/// </summary>
public class ProductPriceChangedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string OldCurrency { get; set; } = "USD";
    public string NewCurrency { get; set; } = "USD";

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product was activated in the catalog.
/// Event: ProductActivated { productId }
/// </summary>
public class ProductActivatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product was deactivated from the catalog.
/// Event: ProductDeactivated { productId }
/// </summary>
public class ProductDeactivatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}
