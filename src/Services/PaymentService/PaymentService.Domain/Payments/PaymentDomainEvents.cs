using Shared.Domain.Primitives;

namespace PaymentService.Domain.Payments;

// ── Domain Events ──────────────────────────────────────────────────────────

public sealed record PaymentCompletedDomainEvent(
    Guid PaymentId, Guid OrderId, decimal Amount, string Currency, string TransactionId)
    : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId, Guid OrderId, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record PaymentRefundedDomainEvent(
    Guid PaymentId, Guid OrderId, decimal Amount) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Domain Errors ──────────────────────────────────────────────────────────

public static class PaymentErrors
{
    public static readonly Error InvalidStatusTransition =
        new("Payment.InvalidTransition", "Invalid payment status transition.");
    public static readonly Error CanOnlyRefundCompleted =
        new("Payment.CannotRefund", "Only completed payments can be refunded.");
    public static readonly Error NotFound =
        new("Payment.NotFound", "Payment not found.");
    public static readonly Error GatewayError =
        new("Payment.GatewayError", "Payment gateway error.");
}
