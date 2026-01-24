namespace ECommerceMvp.Shared.Domain;

/// <summary>
/// DTO: Customer information for checkout saga.
/// </summary>
public class CustomerInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>
/// DTO: Shipping address for checkout saga.
/// </summary>
public class ShippingAddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "US";
}

/// <summary>
/// DTO: Cart item snapshot from Cart service.
/// </summary>
public class CartItemSnapshotDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// DTO: Product snapshot from ProductCatalog service.
/// </summary>
public class ProductSnapshotDto
{
    public string ProductId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO: Stock validation result from Inventory service.
/// </summary>
public class StockValidationResultDto
{
    public string ProductId { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}
