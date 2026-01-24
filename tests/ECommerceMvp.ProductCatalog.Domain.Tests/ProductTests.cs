using ECommerceMvp.ProductCatalog.Domain;
using Xunit;

namespace ECommerceMvp.ProductCatalog.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateProductWithCreatedEvent()
    {
        // Arrange
        var productId = "PROD-001";
        var name = "Test Product";
        var description = "A test product";
        var sku = new Sku("SKU-001");
        var price = new Price(99.99m, "USD");

        // Act
        var product = Product.Create(productId, name, description, sku, price);

        // Assert
        Assert.Equal(productId, product.Id);
        Assert.Equal(name, product.Name);
        Assert.Equal(description, product.Description);
        Assert.Equal(sku, product.Sku);
        Assert.Equal(price, product.Price);
        Assert.True(product.IsActive);
        Assert.NotEmpty(product.UncommittedEvents);
        Assert.IsType<ProductCreatedEvent>(product.UncommittedEvents.First());
    }

    [Fact]
    public void Create_WithEmptyProductId_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Product.Create(string.Empty, "Name", "Desc", new Sku("SKU"), new Price(10)));
    }

    [Fact]
    public void Create_WithNullName_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Product.Create("ID", null!, "Desc", new Sku("SKU"), new Price(10)));
    }

    [Fact]
    public void Create_WithNegativePrice_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Product.Create("ID", "Name", "Desc", new Sku("SKU"), new Price(-10)));
    }

    [Fact]
    public void Activate_WhenInactive_ShouldActivateAndPublishEvent()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Test", "Desc", new Sku("SKU"), new Price(50));
        product.ClearUncommittedEvents();
        product.Deactivate();
        product.ClearUncommittedEvents();

        // Act
        product.Activate();

        // Assert
        Assert.True(product.IsActive);
        Assert.NotEmpty(product.UncommittedEvents);
        Assert.IsType<ProductActivatedEvent>(product.UncommittedEvents.First());
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Test", "Desc", new Sku("SKU"), new Price(50));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => product.Activate());
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldDeactivateAndPublishEvent()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Test", "Desc", new Sku("SKU"), new Price(50));
        product.ClearUncommittedEvents();

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
        Assert.NotEmpty(product.UncommittedEvents);
        Assert.IsType<ProductDeactivatedEvent>(product.UncommittedEvents.First());
    }

    [Fact]
    public void Update_WithValidData_ShouldUpdateAndPublishEvent()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Original", "Desc", new Sku("SKU"), new Price(50));
        product.ClearUncommittedEvents();

        var newPrice = new Price(75.50m, "USD");

        // Act
        product.Update("Updated Name", "Updated Desc", newPrice);

        // Assert
        Assert.Equal("Updated Name", product.Name);
        Assert.Equal("Updated Desc", product.Description);
        Assert.Equal(newPrice, product.Price);
        Assert.NotEmpty(product.UncommittedEvents);
        Assert.IsType<ProductUpdatedEvent>(product.UncommittedEvents.First());
    }

    [Fact]
    public void Update_WhenInactive_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Test", "Desc", new Sku("SKU"), new Price(50));
        product.Deactivate();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            product.Update("Name", "Desc", new Price(100)));
    }
}

public class SkuTests
{
    [Fact]
    public void Create_WithValidValue_ShouldCreateSku()
    {
        // Arrange & Act
        var sku = new Sku("SKU-001");

        // Assert
        Assert.Equal("SKU-001", sku.Value);
    }

    [Fact]
    public void Create_WithEmptyValue_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Sku(string.Empty));
    }

    [Fact]
    public void Equality_ShouldCompareByValue()
    {
        // Arrange
        var sku1 = new Sku("SKU-001");
        var sku2 = new Sku("SKU-001");
        var sku3 = new Sku("SKU-002");

        // Assert
        Assert.Equal(sku1, sku2);
        Assert.NotEqual(sku1, sku3);
    }
}

public class PriceTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldCreatePrice()
    {
        // Arrange & Act
        var price = new Price(99.99m, "USD");

        // Assert
        Assert.Equal(99.99m, price.Amount);
        Assert.Equal("USD", price.Currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Price(-10, "USD"));
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Price(10, string.Empty));
    }

    [Fact]
    public void Equality_ShouldCompareByValue()
    {
        // Arrange
        var price1 = new Price(100, "USD");
        var price2 = new Price(100, "USD");
        var price3 = new Price(100, "EUR");

        // Assert
        Assert.Equal(price1, price2);
        Assert.NotEqual(price1, price3);
    }
}
