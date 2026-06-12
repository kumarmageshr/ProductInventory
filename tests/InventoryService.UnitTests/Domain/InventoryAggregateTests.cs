using FluentAssertions;
using InventoryService.Domain.Inventory;
using Xunit;

namespace InventoryService.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class InventoryAggregateTests
{
    [Fact]
    public void Reserve_WithSufficientStock_ShouldSucceed()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 100);
        var orderId = Guid.NewGuid();

        // Act
        var result = inventory.Reserve(orderId, 30);

        // Assert
        result.IsSuccess.Should().BeTrue();
        inventory.QuantityReserved.Should().Be(30);
        inventory.QuantityAvailable.Should().Be(70);
        inventory.QuantityOnHand.Should().Be(100);
    }

    [Fact]
    public void Reserve_WithInsufficientStock_ShouldFail()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 10);

        // Act
        var result = inventory.Reserve(Guid.NewGuid(), 20);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.InsufficientStock");
    }

    [Fact]
    public void Reserve_ShouldRaiseStockReservedDomainEvent()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 50);
        inventory.ClearDomainEvents();

        // Act
        inventory.Reserve(Guid.NewGuid(), 10);

        // Assert
        inventory.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StockReservedDomainEvent>();
    }

    [Fact]
    public void Release_PreviouslyReserved_ShouldSucceed()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 100);
        var orderId = Guid.NewGuid();
        inventory.Reserve(orderId, 50);
        inventory.ClearDomainEvents();

        // Act
        var result = inventory.Release(orderId, 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        inventory.QuantityReserved.Should().Be(0);
        inventory.QuantityAvailable.Should().Be(100);
        inventory.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StockReleasedDomainEvent>();
    }

    [Fact]
    public void Adjust_PositiveAdjustment_ShouldIncreaseStock()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 100);
        inventory.ClearDomainEvents();

        // Act
        var result = inventory.Adjust(50, "Restock");

        // Assert
        result.IsSuccess.Should().BeTrue();
        inventory.QuantityOnHand.Should().Be(150);
    }

    [Fact]
    public void Adjust_WouldGoNegative_ShouldFail()
    {
        // Arrange
        var inventory = Inventory.Create(Guid.NewGuid(), 10);

        // Act
        var result = inventory.Adjust(-20, "Error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.NegativeStock");
    }
}
