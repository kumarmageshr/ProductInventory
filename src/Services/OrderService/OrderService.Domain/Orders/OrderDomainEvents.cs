using Shared.Domain.Primitives;

namespace OrderService.Domain.Orders;

// ── Domain Events ──────────────────────────────────────────────────────────

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    Guid CustomerId,
    List<(Guid ProductId, int Quantity)> Items,
    decimal TotalAmount,
    string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OrderCompletedDomainEvent(Guid OrderId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OrderCancelledDomainEvent(Guid OrderId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Domain Errors ──────────────────────────────────────────────────────────

public static class OrderErrors
{
    public static readonly Error OrderMustHaveItems =
        new("Order.NoItems", "An order must contain at least one item.");

    public static readonly Error InvalidStatusTransition =
        new("Order.InvalidTransition", "Invalid order status transition.");

    public static readonly Error CannotCancelCompletedOrder =
        new("Order.CannotCancel", "Cannot cancel a completed or already cancelled order.");

    public static readonly Error NotFound =
        new("Order.NotFound", "Order was not found.");

    public static readonly Error Unauthorized =
        new("Order.Unauthorized", "You are not authorized to access this order.");
}
