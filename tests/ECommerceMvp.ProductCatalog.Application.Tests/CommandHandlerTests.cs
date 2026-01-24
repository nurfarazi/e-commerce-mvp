using ECommerceMvp.ProductCatalog.Application;
using ECommerceMvp.ProductCatalog.Domain;
using ECommerceMvp.ProductCatalog.Infrastructure;
using ECommerceMvp.Shared.Application;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace ECommerceMvp.ProductCatalog.Application.Tests;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IRepository<Product, string>> _mockRepository;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<IIdempotencyStore> _mockIdempotencyStore;
    private readonly Mock<ILogger<CreateProductCommandHandler>> _mockLogger;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _mockRepository = new Mock<IRepository<Product, string>>();
        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockIdempotencyStore = new Mock<IIdempotencyStore>();
        _mockLogger = new Mock<ILogger<CreateProductCommandHandler>>();

        _handler = new CreateProductCommandHandler(
            _mockRepository.Object,
            _mockEventPublisher.Object,
            _mockIdempotencyStore.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldCreateProduct()
    {
        // Arrange
        var command = new CreateProductCommand
        {
            ProductId = "PROD-001",
            Name = "Test Product",
            Description = "A test product",
            Sku = "SKU-001",
            Price = 99.99m,
            Currency = "USD"
        };

        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEventPublisher.Setup(p => p.PublishAsync(It.IsAny<IEnumerable<DomainEventEnvelope>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("PROD-001", result.ProductId);
        Assert.Null(result.Error);

        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventPublisher.Verify(p => p.PublishAsync(It.IsAny<IEnumerable<DomainEventEnvelope>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyProductId_ShouldReturnError()
    {
        // Arrange
        var command = new CreateProductCommand
        {
            ProductId = string.Empty,
            Name = "Test",
            Description = "Desc",
            Sku = "SKU",
            Price = 50m
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);

        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNegativePrice_ShouldReturnError()
    {
        // Arrange
        var command = new CreateProductCommand
        {
            ProductId = "PROD-001",
            Name = "Test",
            Description = "Desc",
            Sku = "SKU",
            Price = -10m
        };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);

        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class ActivateProductCommandHandlerTests
{
    private readonly Mock<IRepository<Product, string>> _mockRepository;
    private readonly Mock<IEventPublisher> _mockEventPublisher;
    private readonly Mock<ILogger<ActivateProductCommandHandler>> _mockLogger;
    private readonly ActivateProductCommandHandler _handler;

    public ActivateProductCommandHandlerTests()
    {
        _mockRepository = new Mock<IRepository<Product, string>>();
        _mockEventPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<ActivateProductCommandHandler>>();

        _handler = new ActivateProductCommandHandler(
            _mockRepository.Object,
            _mockEventPublisher.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidProductId_ShouldActivateProduct()
    {
        // Arrange
        var product = Product.Create("PROD-001", "Test", "Desc", new Sku("SKU"), new Price(50));
        product.Deactivate();

        _mockRepository.Setup(r => r.GetByIdAsync("PROD-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEventPublisher.Setup(p => p.PublishAsync(It.IsAny<IEnumerable<DomainEventEnvelope>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new ActivateProductCommand { ProductId = "PROD-001" };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);

        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventPublisher.Verify(p => p.PublishAsync(It.IsAny<IEnumerable<DomainEventEnvelope>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentProduct_ShouldReturnError()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetByIdAsync("NONEXISTENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var command = new ActivateProductCommand { ProductId = "NONEXISTENT" };

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);

        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
