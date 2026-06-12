using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Order;

public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    string Currency,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record OrderCancelledIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record OrderCompletedIntegrationEvent(
    Guid OrderId,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
