using FluentAssertions;
using ProductService.Domain.Products;
using Xunit;

namespace ProductService.UnitTests.Domain;

/// <summary>
/// Unit tests for Product Aggregate — tests domain logic in isolation.
/// No external dependencies (pure unit tests).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductAggregateTests
{
    // ── Product.Create ─────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var name = "Laptop Pro 15";
        var description = "High-performance laptop";
        var price = 1299.99m;
        var currency = "USD";
        var categoryId = Guid.NewGuid();
        var sku = "LAPTOP-PRO-15";

        // Act
        var result = Product.Create(name, description, price, currency, categoryId, sku);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
        result.Value.Price.Amount.Should().Be(price);
        result.Value.Price.Currency.Should().Be("USD");
        result.Value.Status.Should().Be(ProductStatus.Active);
        result.Value.Sku.Value.Should().Be(sku.ToUpperInvariant());
    }

    [Fact]
    public void Create_WithEmptyName_ShouldFail()
    {
        // Act
        var result = Product.Create("", "Description", 99.99m, "USD", Guid.NewGuid(), "SKU-001");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.NameRequired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidPrice_ShouldFail(decimal invalidPrice)
    {
        // Act
        var result = Product.Create("Product", "Description", invalidPrice, "USD", Guid.NewGuid(), "SKU-001");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.InvalidPrice");
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        // Act
        var result = Product.Create("Product", "Desc", 10m, "USD", Guid.NewGuid(), "SKU-001");

        // Assert
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductCreatedDomainEvent>();
    }

    // ── Product.UpdatePrice ────────────────────────────────────────────────

    [Fact]
    public void UpdatePrice_WithValidPrice_ShouldUpdateAndRaiseEvent()
    {
        // Arrange
        var product = CreateValidProduct();
        var newPrice = 999.99m;

        // Act
        product.ClearDomainEvents();
        var result = product.UpdatePrice(newPrice, "USD");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Price.Amount.Should().Be(newPrice);
        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductPriceChangedDomainEvent>();
    }

    [Fact]
    public void UpdatePrice_WithZeroPrice_ShouldFail()
    {
        // Arrange
        var product = CreateValidProduct();

        // Act
        var result = product.UpdatePrice(0, "USD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.InvalidPrice");
    }

    // ── Product.Deactivate ─────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ActiveProduct_ShouldSucceed()
    {
        // Arrange
        var product = CreateValidProduct();

        // Act
        var result = product.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Inactive);
    }

    // ── Money Value Object ─────────────────────────────────────────────────

    [Fact]
    public void Money_SameAmountAndCurrency_ShouldBeEqual()
    {
        // Arrange
        var money1 = new Money(100m, "USD");
        var money2 = new Money(100m, "USD");

        // Assert
        money1.Should().Be(money2);
    }

    [Fact]
    public void Money_DifferentCurrencies_ShouldNotBeEqual()
    {
        // Arrange
        var money1 = new Money(100m, "USD");
        var money2 = new Money(100m, "EUR");

        // Assert
        money1.Should().NotBe(money2);
    }

    [Fact]
    public void Money_Add_SameCurrencies_ShouldSucceed()
    {
        // Arrange
        var price1 = new Money(100m, "USD");
        var price2 = new Money(50m, "USD");

        // Act
        var total = price1.Add(price2);

        // Assert
        total.Amount.Should().Be(150m);
        total.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Add_DifferentCurrencies_ShouldThrow()
    {
        // Arrange
        var price1 = new Money(100m, "USD");
        var price2 = new Money(50m, "EUR");

        // Act & Assert
        var act = () => price1.Add(price2);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add different currencies.");
    }

    // ── Sku Value Object ───────────────────────────────────────────────────

    [Fact]
    public void Sku_Creation_ShouldUppercase()
    {
        // Arrange & Act
        var sku = new Sku("laptop-pro");

        // Assert
        sku.Value.Should().Be("LAPTOP-PRO");
    }

    [Fact]
    public void Sku_Empty_ShouldThrow()
    {
        // Act & Assert
        var act = () => new Sku("");
        act.Should().Throw<ArgumentException>();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Product CreateValidProduct() =>
        Product.Create("Test Product", "Description", 99.99m, "USD", Guid.NewGuid(), "TEST-001")
            .Value;
}
