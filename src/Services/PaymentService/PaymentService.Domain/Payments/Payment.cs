using Shared.Domain.Primitives;

namespace PaymentService.Domain.Payments;

/// <summary>
/// Payment Aggregate Root.
/// </summary>
public sealed class Payment : AggregateRoot<PaymentId>
{
    private Payment() { }

    private Payment(
        PaymentId id,
        Guid orderId,
        Money amount,
        string provider)
    {
        Id = id;
        OrderId = orderId;
        Amount = amount;
        Provider = provider;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public Money Amount { get; private set; } = default!;
    public string Provider { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? TransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? ProcessedAt { get; private set; }

    public static Payment Create(Guid orderId, decimal amount, string currency, string provider)
    {
        var payment = new Payment(
            PaymentId.New(),
            orderId,
            new Money(amount, currency),
            provider);

        return payment;
    }

    public Result Complete(string transactionId)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentErrors.InvalidStatusTransition);

        Status = PaymentStatus.Completed;
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentCompletedDomainEvent(
            Id.Value, OrderId, Amount.Amount, Amount.Currency, transactionId));

        return Result.Success();
    }

    public Result Fail(string reason)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentErrors.InvalidStatusTransition);

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        ProcessedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PaymentFailedDomainEvent(Id.Value, OrderId, reason));
        return Result.Success();
    }

    public Result Refund()
    {
        if (Status != PaymentStatus.Completed)
            return Result.Failure(PaymentErrors.CanOnlyRefundCompleted);

        Status = PaymentStatus.Refunded;
        RaiseDomainEvent(new PaymentRefundedDomainEvent(
            Id.Value, OrderId, Amount.Amount));
        return Result.Success();
    }
}

public enum PaymentStatus { Pending, Completed, Failed, Refunded }

public record PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public static PaymentId From(Guid v) => new(v);
}

public sealed class Money : ValueObject
{
    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency.ToUpperInvariant();
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
