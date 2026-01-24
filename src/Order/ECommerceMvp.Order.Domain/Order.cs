using ECommerceMvp.Shared.Domain;

namespace ECommerceMvp.Order.Domain;

/// <summary>
/// Value object for order ID (unique identifier).
/// </summary>
public class OrderId : ValueObject
{
    public string Value { get; }

    public OrderId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Order ID cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(OrderId orderId) => orderId.Value;
    public static implicit operator OrderId(string value) => new(value);
}

/// <summary>
/// Value object for order number (human-readable identifier).
/// Example: ORD-20250124-001
/// </summary>
public class OrderNumber : ValueObject
{
    public string Value { get; }

    public OrderNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Order number cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(OrderNumber orderNumber) => orderNumber.Value;
    public static implicit operator OrderNumber(string value) => new(value);
}

/// <summary>
/// Value object for guest token (session identifier for guest checkouts).
/// </summary>
public class GuestToken : ValueObject
{
    public string Value { get; }

    public GuestToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Guest token cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(GuestToken token) => token.Value;
    public static implicit operator GuestToken(string value) => new(value);
}

/// <summary>
/// Value object for customer information.
/// Constraints: name and phone are required; email is optional.
/// </summary>
public class CustomerInfo : ValueObject
{
    private const int MinNameLength = 2;

    public string Name { get; }
    public string Phone { get; }
    public string? Email { get; }

    public CustomerInfo(string name, string phone, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name cannot be empty", nameof(name));
        if (name.Length < MinNameLength)
            throw new ArgumentException($"Customer name must be at least {MinNameLength} characters", nameof(name));

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Customer phone cannot be empty", nameof(phone));

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            throw new ArgumentException("Invalid email format", nameof(email));

        Name = name.Trim();
        Phone = phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Phone;
        yield return Email;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Value object for shipping address.
/// Constraints: line1 and city are required; postalCode is optional; country defaults to a valid value.
/// </summary>
public class ShippingAddress : ValueObject
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string? PostalCode { get; }
    public string Country { get; }

    public ShippingAddress(string line1, string city, string? line2 = null, string? postalCode = null, string country = "US")
    {
        if (string.IsNullOrWhiteSpace(line1))
            throw new ArgumentException("Address line 1 cannot be empty", nameof(line1));

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty", nameof(city));

        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty", nameof(country));

        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        Country = country.Trim();
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return PostalCode;
        yield return Country;
    }
}

/// <summary>
/// Value object for money (amount and currency).
/// </summary>
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative", nameof(amount));
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

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add money in different currencies");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot subtract money in different currencies");
        return new Money(left.Amount - right.Amount, left.Currency);
    }
}

/// <summary>
/// Value object for order totals (subtotal, shipping fee, total).
/// Constraints: shippingFee is always 0 in MVP.
/// </summary>
public class OrderTotals : ValueObject
{
    public Money Subtotal { get; }
    public Money ShippingFee { get; }
    public Money Total { get; }

    public OrderTotals(Money subtotal, Money? shippingFee = null)
    {
        if (subtotal == null)
            throw new ArgumentNullException(nameof(subtotal));

        // Shipping fee is always 0 in MVP
        ShippingFee = shippingFee ?? new Money(0, subtotal.Currency);

        if (ShippingFee.Amount != 0)
            throw new InvalidOperationException("Shipping fee must be 0 in MVP");

        if (ShippingFee.Currency != subtotal.Currency)
            throw new InvalidOperationException("Shipping fee must use same currency as subtotal");

        Subtotal = subtotal;
        Total = Subtotal + ShippingFee;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Subtotal;
        yield return ShippingFee;
        yield return Total;
    }
}

/// <summary>
/// Value object for idempotency key to prevent duplicate order creation.
/// </summary>
public class IdempotencyKey : ValueObject
{
    public string Value { get; }

    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Idempotency key cannot be empty", nameof(value));

        Value = value;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(IdempotencyKey key) => key.Value;
    public static implicit operator IdempotencyKey(string value) => new(value);
}

/// <summary>
/// Entity representing a line item in an order.
/// Immutable snapshot of product at order time.
/// </summary>
public class OrderLineItem : Entity<string>
{
    public string ProductId { get; }
    public string SkuSnapshot { get; }
    public string NameSnapshot { get; }
    public Money UnitPriceSnapshot { get; }
    public int Quantity { get; }
    public Money LineTotal { get; }

    public OrderLineItem(
        string id,
        string productId,
        string skuSnapshot,
        string nameSnapshot,
        Money unitPriceSnapshot,
        int quantity)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("ProductId cannot be empty", nameof(productId));
        if (string.IsNullOrWhiteSpace(skuSnapshot))
            throw new ArgumentException("SKU snapshot cannot be empty", nameof(skuSnapshot));
        if (string.IsNullOrWhiteSpace(nameSnapshot))
            throw new ArgumentException("Name snapshot cannot be empty", nameof(nameSnapshot));
        if (unitPriceSnapshot == null)
            throw new ArgumentNullException(nameof(unitPriceSnapshot));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));

        ProductId = productId;
        SkuSnapshot = skuSnapshot;
        NameSnapshot = nameSnapshot;
        UnitPriceSnapshot = unitPriceSnapshot;
        Quantity = quantity;
        LineTotal = new Money(unitPriceSnapshot.Amount * quantity, unitPriceSnapshot.Currency);
    }
}

/// <summary>
/// Order aggregate root in the Order bounded context.
/// Invariants:
/// - Order must have at least one line item
/// - All products must be active at order time
/// - Requested quantities must be available at commit time
/// - Customer name and phone are required
/// - Address line1 and city are required
/// - Shipping fee is always 0
/// - Payment method is always COD
/// - PaymentStatus is always Pending
/// - Idempotency key must not create duplicate orders
/// </summary>
public class Order : AggregateRoot<string>
{
    private List<OrderLineItem> _lineItems = [];

    public OrderNumber OrderNumber { get; private set; } = null!;
    public GuestToken GuestToken { get; private set; } = null!;
    public string CartId { get; private set; } = string.Empty;
    public CustomerInfo CustomerInfo { get; private set; } = null!;
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    public OrderTotals Totals { get; private set; } = null!;
    public string PaymentMethod { get; private set; } = "COD"; // Cash On Delivery
    public string PaymentStatus { get; private set; } = "Pending";
    public OrderStatus Status { get; private set; } = OrderStatus.Created;
    public bool StockCommitted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyList<OrderLineItem> LineItems => _lineItems.AsReadOnly();

    private Order(string id) : base(id)
    {
    }

    /// <summary>
    /// Factory method to create a new order from cart and product snapshots.
    /// Validates all invariants before creation.
    /// </summary>
    public static Order PlaceFromCart(
        string orderId,
        string orderNumber,
        GuestToken guestToken,
        string cartId,
        List<OrderLineItem> lineItems,
        CustomerInfo customerInfo,
        ShippingAddress shippingAddress,
        Money subtotal)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID cannot be empty", nameof(orderId));
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number cannot be empty", nameof(orderNumber));
        if (guestToken == null)
            throw new ArgumentNullException(nameof(guestToken));
        if (string.IsNullOrWhiteSpace(cartId))
            throw new ArgumentException("Cart ID cannot be empty", nameof(cartId));
        if (lineItems == null || lineItems.Count == 0)
            throw new ArgumentException("Order must have at least one line item", nameof(lineItems));
        if (customerInfo == null)
            throw new ArgumentNullException(nameof(customerInfo));
        if (shippingAddress == null)
            throw new ArgumentNullException(nameof(shippingAddress));
        if (subtotal == null)
            throw new ArgumentNullException(nameof(subtotal));

        var order = new Order(orderId)
        {
            OrderNumber = new OrderNumber(orderNumber),
            GuestToken = guestToken,
            CartId = cartId,
            CustomerInfo = customerInfo,
            ShippingAddress = shippingAddress,
            Totals = new OrderTotals(subtotal),
            Status = OrderStatus.Created,
            StockCommitted = false,
            CreatedAt = DateTime.UtcNow,
            _lineItems = lineItems
        };

        return order;
    }

    /// <summary>
    /// Mark stock as committed after successful inventory deduction.
    /// </summary>
    public void MarkStockCommitted()
    {
        if (StockCommitted)
            throw new InvalidOperationException("Stock already committed");

        StockCommitted = true;
        Status = OrderStatus.StockCommitted;
        AddUncommittedEvent(new OrderStockCommittedEvent
        {
            AggregateId = Id,
            OrderId = Id,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Mark order as fully finalized.
    /// </summary>
    public void FinalizeCreated()
    {
        if (Status != OrderStatus.StockCommitted)
            throw new InvalidOperationException("Order must have stock committed before finalization");

        Status = OrderStatus.Finalized;
        AddUncommittedEvent(new OrderFinalizedEvent
        {
            AggregateId = Id,
            OrderId = Id,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Enum for order status.
/// </summary>
public enum OrderStatus
{
    Created = 1,
    Validated = 2,
    Priced = 3,
    StockCommitRequested = 4,
    StockCommitted = 5,
    Finalized = 6
}
