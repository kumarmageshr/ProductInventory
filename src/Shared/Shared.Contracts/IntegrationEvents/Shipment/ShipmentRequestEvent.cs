using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Shipment;

// Additional events needed by the Saga Orchestrator

public sealed record ShipmentRequestedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);
