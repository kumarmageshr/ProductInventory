using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Shipment;

public sealed record ShipmentCreatedIntegrationEvent(
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record ShipmentFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record DeliveryConfirmedIntegrationEvent(
    Guid ShipmentId,
    Guid OrderId,
    DateTime DeliveredAt,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);
