using MediatR;
using PaymentService.Domain.Payments;
using Shared.Contracts.IntegrationEvents.Payment;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Messaging;

namespace PaymentService.Application.Payments.EventHandlers;

/// <summary>
/// Handles PaymentRequested integration event from the Order Saga.
/// Processes payment and publishes result back to the bus.
/// Decorator Pattern is applied at registration time: LoggingDecorator wraps this handler.
/// </summary>
public sealed class PaymentRequestedIntegrationEventHandler
    : IIntegrationEventHandler<PaymentRequestedIntegrationEvent>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentGatewayFactory _gatewayFactory;
    private readonly IEventBus _eventBus;

    public PaymentRequestedIntegrationEventHandler(
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        IPaymentGatewayFactory gatewayFactory,
        IEventBus eventBus)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _gatewayFactory = gatewayFactory;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(
        PaymentRequestedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        // Strategy Pattern: select payment provider from config or order context
        var gateway = _gatewayFactory.Create("Stripe"); // configurable per merchant

        var payment = Payment.Create(
            @event.OrderId,
            @event.Amount,
            @event.Currency,
            gateway.ProviderName);

        _paymentRepository.Add(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Call the payment gateway adapter
        var result = await gateway.ChargeAsync(
            customerId: @event.OrderId.ToString(), // simplified; real: lookup customer
            amount: @event.Amount,
            currency: @event.Currency,
            idempotencyKey: @event.EventId.ToString(),
            cancellationToken);

        if (result.IsSuccess)
        {
            payment.Complete(result.TransactionId!);
            _paymentRepository.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new PaymentCompletedIntegrationEvent(
                payment.Id.Value, @event.OrderId,
                @event.Amount, @event.Currency,
                @event.CorrelationId, @event.TraceId), cancellationToken);
        }
        else
        {
            payment.Fail(result.ErrorMessage ?? "Gateway error");
            _paymentRepository.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new PaymentFailedIntegrationEvent(
                @event.OrderId, result.ErrorMessage ?? "Payment gateway error",
                @event.CorrelationId, @event.TraceId), cancellationToken);
        }
    }
}

public interface IPaymentRepository : IRepository<Payment, PaymentId>
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}
