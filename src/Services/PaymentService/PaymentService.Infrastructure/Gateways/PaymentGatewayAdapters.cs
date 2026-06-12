using Microsoft.Extensions.Logging;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Gateways;

/// <summary>
/// Adapter Pattern — adapts Stripe SDK to IPaymentGateway.
/// The domain layer never references Stripe directly.
/// </summary>
public sealed class StripeGatewayAdapter : IPaymentGateway
{
    private readonly ILogger<StripeGatewayAdapter> _logger;
    // In production: inject Stripe.StripeClient

    public StripeGatewayAdapter(ILogger<StripeGatewayAdapter> logger) =>
        _logger = logger;

    public string ProviderName => "Stripe";

    public async Task<PaymentGatewayResult> ChargeAsync(
        string customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Stripe: Charging customer {CustomerId} amount {Amount} {Currency}",
            customerId, amount, currency);

        // Production: call Stripe SDK
        // var options = new ChargeCreateOptions { Amount = (long)(amount * 100), Currency = currency, ... };
        // var service = new ChargeService();
        // var charge = await service.CreateAsync(options, new RequestOptions { IdempotencyKey = idempotencyKey });

        await Task.Delay(50, cancellationToken); // Simulate network call

        return new PaymentGatewayResult(
            IsSuccess: true,
            TransactionId: $"stripe_txn_{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task<PaymentGatewayResult> RefundAsync(
        string transactionId, decimal amount, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stripe: Refunding transaction {TransactionId}", transactionId);
        await Task.Delay(50, cancellationToken);

        return new PaymentGatewayResult(
            IsSuccess: true,
            TransactionId: $"stripe_refund_{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task<bool> VerifyAsync(
        string transactionId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);
        return !string.IsNullOrEmpty(transactionId);
    }
}

/// <summary>
/// Adapter Pattern — adapts PayPal SDK to IPaymentGateway.
/// </summary>
public sealed class PayPalGatewayAdapter : IPaymentGateway
{
    private readonly ILogger<PayPalGatewayAdapter> _logger;

    public PayPalGatewayAdapter(ILogger<PayPalGatewayAdapter> logger) =>
        _logger = logger;

    public string ProviderName => "PayPal";

    public async Task<PaymentGatewayResult> ChargeAsync(
        string customerId, decimal amount, string currency,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PayPal: Charging {Amount} {Currency}", amount, currency);
        await Task.Delay(60, cancellationToken);

        return new PaymentGatewayResult(
            IsSuccess: true,
            TransactionId: $"paypal_txn_{Guid.NewGuid():N}",
            ErrorCode: null,
            ErrorMessage: null);
    }

    public async Task<PaymentGatewayResult> RefundAsync(
        string transactionId, decimal amount, CancellationToken cancellationToken = default)
    {
        await Task.Delay(60, cancellationToken);
        return new PaymentGatewayResult(true, $"paypal_ref_{Guid.NewGuid():N}", null, null);
    }

    public async Task<bool> VerifyAsync(
        string transactionId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);
        return true;
    }
}

/// <summary>
/// Factory Pattern — creates the correct IPaymentGateway based on provider name.
/// </summary>
public sealed class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways) =>
        _gateways = gateways;

    public IPaymentGateway Create(string providerName) =>
        _gateways.FirstOrDefault(g =>
            g.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Payment provider '{providerName}' not registered.");
}
