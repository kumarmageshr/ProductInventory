using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Payment;

// Additional events needed by the Saga Orchestrator

public sealed record PaymentRequestedIntegrationEvent(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record RefundRequestedIntegrationEvent(
    Guid OrderId,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);
