using FluentAssertions;
using OrderService.Domain.Orders;
using Xunit;

namespace OrderService.UnitTests.Domain;

/// <summary>
/// Unit tests for Order Aggregate — verifies the order state machine.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrderAggregateTests
{
    [Fact]
    public void Create_WithValidItems_ShouldSucceed()
    {
        // Arrange
        var items = new List<(Guid, string, int, decimal)>
        {
            (Guid.NewGuid(), "Laptop", 1, 999.99m),
            (Guid.NewGuid(), "Mouse", 2, 29.99m)
        };

        // Act
        var result = Order.Create(Guid.NewGuid(), items, "USD");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OrderStatus.Pending);
        result.Value.Items.Count.Should().Be(2);
        result.Value.TotalAmount.Amount.Should().Be(1059.97m);
    }

    [Fact]
    public void Create_WithNoItems_ShouldFail()
    {
        // Act
        var result = Order.Create(Guid.NewGuid(), [], "USD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.NoItems");
    }

    [Fact]
    public void Create_ShouldRaiseOrderCreatedDomainEvent()
    {
        // Arrange
        var items = new List<(Guid, string, int, decimal)>
        {
            (Guid.NewGuid(), "Product", 1, 50m)
        };

        // Act
        var result = Order.Create(Guid.NewGuid(), items, "USD");

        // Assert
        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void StateTransition_PendingToInventoryReserved_ShouldSucceed()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        var result = order.MarkInventoryReserved();

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InventoryReserved);
    }

    [Fact]
    public void StateTransition_CannotSkipInventoryReserved()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act — skip InventoryReserved step
        var result = order.MarkPaymentProcessed();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.InvalidTransition");
    }

    [Fact]
    public void CompleteWorkflow_ShouldTransitionThroughAllStates()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act — full happy path
        order.ClearDomainEvents();
        order.MarkInventoryReserved();
        order.MarkPaymentProcessed();
        order.MarkShipped();
        var result = order.Complete();

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Completed);
        order.CompletedAt.Should().NotBeNull();
        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCompletedDomainEvent>();
    }

    [Fact]
    public void Cancel_PendingOrder_ShouldSucceed()
    {
        // Arrange
        var order = CreateValidOrder();

        // Act
        var result = order.Cancel("Customer requested cancellation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer requested cancellation");
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_CompletedOrder_ShouldFail()
    {
        // Arrange
        var order = CreateValidOrder();
        order.MarkInventoryReserved();
        order.MarkPaymentProcessed();
        order.MarkShipped();
        order.Complete();

        // Act
        var result = order.Cancel("Too late");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CannotCancel");
    }

    private static Order CreateValidOrder() =>
        Order.Create(
            Guid.NewGuid(),
            [(Guid.NewGuid(), "Product", 1, 100m)],
            "USD").Value;
}
