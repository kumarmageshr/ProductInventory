namespace OrderService.Domain.Sagas;

/// <summary>
/// Order Saga State — persisted to track saga progress across async steps.
/// </summary>
public sealed class OrderSagaState
{
    public Guid SagaId { get; init; } = Guid.NewGuid();
    public Guid OrderId { get; init; }
    public OrderSagaStatus Status { get; set; } = OrderSagaStatus.Started;
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool ShipmentCreated { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
}

public enum OrderSagaStatus
{
    Started,
    InventoryReserving,
    InventoryReserved,
    PaymentProcessing,
    PaymentProcessed,
    ShipmentCreating,
    ShipmentCreated,
    Completed,
    // Compensation states
    CompensatingPayment,
    CompensatingInventory,
    Cancelled
}
