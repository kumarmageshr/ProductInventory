using FluentAssertions;
using Moq;
using ProductService.Application.Products.Commands.CreateProduct;
using ProductService.Domain.Products;
using Shared.Domain.Primitives;
using Xunit;

namespace ProductService.UnitTests.Application;

/// <summary>
/// Unit tests for CreateProductCommandHandler.
/// Uses Moq to isolate from infrastructure.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateProductCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateProductAndReturnId()
    {
        // Arrange
        var command = new CreateProductCommand(
            "Laptop", "High-end laptop", 999.99m, "USD", Guid.NewGuid(), "LAPTOP-001");

        _repositoryMock
            .Setup(r => r.SkuExistsAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        _repositoryMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateProductCommand(
            "Laptop", "Description", 999.99m, "USD", Guid.NewGuid(), "LAPTOP-001");

        _repositoryMock
            .Setup(r => r.SkuExistsAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // SKU already exists

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.SkuAlreadyExists");

        _repositoryMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidPrice_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateProductCommand(
            "Laptop", "Description", -1m, "USD", Guid.NewGuid(), "LAPTOP-001");

        _repositoryMock
            .Setup(r => r.SkuExistsAsync(command.Sku, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.InvalidPrice");
    }
}
