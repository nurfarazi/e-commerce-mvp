using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.ProductCatalog.Domain;

/// <summary>
/// Domain event: Product was created in the catalog.
/// </summary>
public class ProductCreatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product was updated.
/// </summary>
public class ProductUpdatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product was activated in the catalog.
/// </summary>
public class ProductActivatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}

/// <summary>
/// Domain event: Product was deactivated from the catalog.
/// </summary>
public class ProductDeactivatedEvent : DomainEvent
{
    public string ProductId { get; set; } = string.Empty;

    public override int EventVersion => 1;
}
