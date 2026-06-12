using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Inventory;

// Additional events needed by the Saga Orchestrator

public sealed record InventoryReleaseRequestedIntegrationEvent(
    Guid OrderId,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);
