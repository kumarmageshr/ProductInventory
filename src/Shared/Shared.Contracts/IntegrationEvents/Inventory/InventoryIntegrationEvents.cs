using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Inventory;

public sealed record InventoryReservedIntegrationEvent(
    Guid OrderId,
    List<ReservedItemDto> Items,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record InventoryReservationFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record InventoryReleasedIntegrationEvent(
    Guid OrderId,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record ReservedItemDto(Guid ProductId, int Quantity);
