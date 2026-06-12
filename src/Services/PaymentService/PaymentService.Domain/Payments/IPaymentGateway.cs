namespace PaymentService.Domain.Payments;

/// <summary>
/// Adapter Pattern — target interface for external payment gateways.
/// Each payment provider (Stripe, PayPal, RazorPay) implements this interface,
/// making them interchangeable without changing application logic.
///
/// IPaymentGateway
///   └── StripeGatewayAdapter        (wraps Stripe SDK)
///   └── PayPalGatewayAdapter        (wraps PayPal SDK)
///   └── RazorPayGatewayAdapter      (wraps RazorPay SDK)
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<PaymentGatewayResult> ChargeAsync(
        string customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PaymentGatewayResult> RefundAsync(
        string transactionId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(
        string transactionId,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentGatewayResult(
    bool IsSuccess,
    string? TransactionId,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// Factory Pattern — selects the appropriate payment gateway based on configuration.
/// Open/Closed: add new providers without modifying existing code.
/// </summary>
public interface IPaymentGatewayFactory
{
    IPaymentGateway Create(string providerName);
}
