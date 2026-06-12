using Shared.Domain.Primitives;

namespace OrderService.Domain.Orders;

/// <summary>
/// Order Aggregate Root — central consistency boundary.
/// Applies all state transitions through explicit business operations.
/// </summary>
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];

    private Order() { }

    private Order(
        OrderId id,
        CustomerId customerId,
        List<OrderItem> items,
        string currency)
    {
        Id = id;
        CustomerId = customerId;
        _items.AddRange(items);
        TotalAmount = new Money(items.Sum(i => i.UnitPrice.Amount * i.Quantity), currency);
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public CustomerId CustomerId { get; private set; } = default!;
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public Money TotalAmount { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // ── Factory ────────────────────────────────────────────────────────────

    public static Result<Order> Create(
        Guid customerId,
        List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items,
        string currency)
    {
        if (!items.Any())
            return Result.Failure<Order>(OrderErrors.OrderMustHaveItems);

        var orderItems = items.Select(i =>
            OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, currency))
            .ToList();

        var order = new Order(
            OrderId.New(),
            new CustomerId(customerId),
            orderItems,
            currency);

        order.RaiseDomainEvent(new OrderCreatedDomainEvent(
            order.Id.Value,
            customerId,
            order.Items.Select(i => (i.ProductId.Value, i.Quantity)).ToList(),
            order.TotalAmount.Amount,
            currency));

        return order;
    }

    // ── State Machine ──────────────────────────────────────────────────────

    public Result MarkInventoryReserved()
    {
        if (Status != OrderStatus.Pending)
            return Result.Failure(OrderErrors.InvalidStatusTransition);

        Status = OrderStatus.InventoryReserved;
        return Result.Success();
    }

    public Result MarkPaymentProcessed()
    {
        if (Status != OrderStatus.InventoryReserved)
            return Result.Failure(OrderErrors.InvalidStatusTransition);

        Status = OrderStatus.PaymentProcessed;
        return Result.Success();
    }

    public Result MarkShipped()
    {
        if (Status != OrderStatus.PaymentProcessed)
            return Result.Failure(OrderErrors.InvalidStatusTransition);

        Status = OrderStatus.Shipped;
        return Result.Success();
    }

    public Result Complete()
    {
        if (Status != OrderStatus.Shipped)
            return Result.Failure(OrderErrors.InvalidStatusTransition);

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderCompletedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
            return Result.Failure(OrderErrors.CannotCancelCompletedOrder);

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderCancelledDomainEvent(Id.Value, reason));
        return Result.Success();
    }
}

public enum OrderStatus
{
    Pending,
    InventoryReserved,
    PaymentProcessed,
    Shipped,
    Completed,
    Cancelled
}

// ── Order Item ─────────────────────────────────────────────────────────────

public sealed class OrderItem : Entity<OrderItemId>
{
    private OrderItem() { }

    private OrderItem(
        OrderItemId id,
        ProductId productId,
        string productName,
        int quantity,
        Money unitPrice) : base(id)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public ProductId ProductId { get; private set; } = default!;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = default!;

    public static OrderItem Create(
        Guid productId, string productName, int quantity, decimal unitPrice, string currency) =>
        new(OrderItemId.New(), new ProductId(productId), productName, quantity,
            new Money(unitPrice, currency));
}

// ── Strongly-typed IDs ─────────────────────────────────────────────────────

public record OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid v) => new(v);
}

public record OrderItemId(Guid Value)
{
    public static OrderItemId New() => new(Guid.NewGuid());
}

public record CustomerId(Guid Value);
public record ProductId(Guid Value);

// ── Value Objects ──────────────────────────────────────────────────────────

public sealed class Money : ValueObject
{
    public Money(decimal amount, string currency)
    {
        Amount = amount >= 0 ? amount : throw new ArgumentException("Amount cannot be negative.");
        Currency = !string.IsNullOrWhiteSpace(currency)
            ? currency.ToUpperInvariant()
            : throw new ArgumentException("Currency required.");
    }

    private Money() { }

    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = default!;

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }
}
