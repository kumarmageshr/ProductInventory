using Shared.Contracts.Events;

namespace Shared.Contracts.IntegrationEvents.Payment;

public sealed record PaymentCompletedIntegrationEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record PaymentFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);

public sealed record PaymentRefundedIntegrationEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal RefundAmount,
    string CorrelationId,
    string TraceId)
    : IntegrationEvent(CorrelationId, TraceId);
