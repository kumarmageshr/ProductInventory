using Microsoft.Extensions.Logging;
using OrderService.Domain.Orders;
using OrderService.Domain.Sagas;
using Shared.Contracts.IntegrationEvents.Inventory;
using Shared.Contracts.IntegrationEvents.Order;
using Shared.Contracts.IntegrationEvents.Payment;
using Shared.Contracts.IntegrationEvents.Shipment;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Messaging;

namespace OrderService.Application.Orders.Sagas;

/// <summary>
/// Orchestrator-based Saga for the Order Placement workflow.
///
/// WHY ORCHESTRATOR over Choreography?
///   - Single place to understand the complete workflow
///   - Easier to implement compensation (rollback) flows
///   - Clearer visibility into saga state
///   - Services remain decoupled from each other (only talk to orchestrator)
///   - Better suited when workflow logic is complex
///
/// Trade-off: The Order Service becomes a coordination hub (slight coupling),
/// but this is acceptable because Order owns the business process.
///
/// Workflow:
///   OrderCreated → ReserveInventory → ProcessPayment → CreateShipment → Complete
///
/// Compensation:
///   ShipmentFailed → RefundPayment → ReleaseInventory → CancelOrder
/// </summary>
public sealed class OrderSagaOrchestrator
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderSagaStateRepository _sagaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderSagaOrchestrator> _logger;

    public OrderSagaOrchestrator(
        IOrderRepository orderRepository,
        IOrderSagaStateRepository sagaRepository,
        IUnitOfWork unitOfWork,
        IEventBus eventBus,
        ILogger<OrderSagaOrchestrator> logger)
    {
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
        _unitOfWork = unitOfWork;
        _eventBus = eventBus;
        _logger = logger;
    }

    // ── Step 1: Saga starts when order is created ─────────────────────────

    public async Task StartAsync(Order order, string correlationId, string traceId,
        CancellationToken ct = default)
    {
        var sagaState = new OrderSagaState
        {
            OrderId = order.Id.Value,
            Status = OrderSagaStatus.InventoryReserving,
            CorrelationId = correlationId,
            TraceId = traceId
        };

        _sagaRepository.Add(sagaState);

        // Publish to inventory-sub subscription
        await _eventBus.PublishAsync(new OrderCreatedIntegrationEvent(
            order.Id.Value,
            order.CustomerId.Value,
            order.Items.Select(i => new Shared.Contracts.IntegrationEvents.Order.OrderItemDto(
                i.ProductId.Value, i.ProductName, i.Quantity, i.UnitPrice.Amount)).ToList(),
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            correlationId,
            traceId), ct);

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Saga started for Order {OrderId}", order.Id.Value);
    }

    // ── Step 2: Inventory Reserved → trigger Payment ──────────────────────

    public async Task HandleInventoryReservedAsync(
        InventoryReservedIntegrationEvent @event, CancellationToken ct = default)
    {
        var sagaState = await _sagaRepository.GetByOrderIdAsync(@event.OrderId, ct)
            ?? throw new InvalidOperationException($"Saga not found for Order {@event.OrderId}");

        sagaState.InventoryReserved = true;
        sagaState.Status = OrderSagaStatus.PaymentProcessing;
        sagaState.UpdatedAt = DateTime.UtcNow;

        var order = await _orderRepository.GetByIdAsync(OrderId.From(@event.OrderId), ct)!;
        order!.MarkInventoryReserved();
        _orderRepository.Update(order);

        await _eventBus.PublishAsync(new Shared.Contracts.IntegrationEvents.Payment.PaymentRequestedIntegrationEvent(
            @event.OrderId,
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            @event.CorrelationId,
            @event.TraceId), ct);

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Inventory reserved for Order {OrderId}, triggering payment", @event.OrderId);
    }

    // ── Step 3: Payment Completed → trigger Shipment ──────────────────────

    public async Task HandlePaymentCompletedAsync(
        PaymentCompletedIntegrationEvent @event, CancellationToken ct = default)
    {
        var sagaState = await _sagaRepository.GetByOrderIdAsync(@event.OrderId, ct)!;
        sagaState!.PaymentProcessed = true;
        sagaState.Status = OrderSagaStatus.ShipmentCreating;
        sagaState.UpdatedAt = DateTime.UtcNow;

        var order = await _orderRepository.GetByIdAsync(OrderId.From(@event.OrderId), ct)!;
        order!.MarkPaymentProcessed();
        _orderRepository.Update(order);

        await _eventBus.PublishAsync(new Shared.Contracts.IntegrationEvents.Shipment.ShipmentRequestedIntegrationEvent(
            @event.OrderId,
            order.CustomerId.Value,
            @event.CorrelationId,
            @event.TraceId), ct);

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Payment completed for Order {OrderId}, triggering shipment", @event.OrderId);
    }

    // ── Step 4: Shipment Created → Order Completed ────────────────────────

    public async Task HandleShipmentCreatedAsync(
        ShipmentCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        var sagaState = await _sagaRepository.GetByOrderIdAsync(@event.OrderId, ct)!;
        sagaState!.ShipmentCreated = true;
        sagaState.Status = OrderSagaStatus.Completed;
        sagaState.UpdatedAt = DateTime.UtcNow;

        var order = await _orderRepository.GetByIdAsync(OrderId.From(@event.OrderId), ct)!;
        order!.MarkShipped();
        order.Complete();
        _orderRepository.Update(order);

        await _eventBus.PublishAsync(new Shared.Contracts.IntegrationEvents.Order.OrderCompletedIntegrationEvent(
            @event.OrderId, @event.CorrelationId, @event.TraceId), ct);

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Order {OrderId} completed successfully", @event.OrderId);
    }

    // ── Compensation: Inventory Reservation Failed ─────────────────────────

    public async Task HandleInventoryReservationFailedAsync(
        InventoryReservationFailedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Inventory reservation failed for Order {OrderId}: {Reason}",
            @event.OrderId, @event.Reason);

        await CancelOrderAsync(@event.OrderId, @event.Reason,
            @event.CorrelationId, @event.TraceId, ct);
    }

    // ── Compensation: Payment Failed ───────────────────────────────────────

    public async Task HandlePaymentFailedAsync(
        PaymentFailedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Payment failed for Order {OrderId}: {Reason}. Releasing inventory.",
            @event.OrderId, @event.Reason);

        var sagaState = await _sagaRepository.GetByOrderIdAsync(@event.OrderId, ct)!;
        sagaState!.Status = OrderSagaStatus.CompensatingInventory;
        sagaState.FailureReason = @event.Reason;

        // Trigger inventory release
        await _eventBus.PublishAsync(new InventoryReleaseRequestedIntegrationEvent(
            @event.OrderId, @event.CorrelationId, @event.TraceId), ct);

        await CancelOrderAsync(@event.OrderId, @event.Reason,
            @event.CorrelationId, @event.TraceId, ct);
    }

    // ── Compensation: Shipment Failed ──────────────────────────────────────

    public async Task HandleShipmentFailedAsync(
        ShipmentFailedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Shipment failed for Order {OrderId}: {Reason}. Compensating payment and inventory.",
            @event.OrderId, @event.Reason);

        var sagaState = await _sagaRepository.GetByOrderIdAsync(@event.OrderId, ct)!;
        sagaState!.Status = OrderSagaStatus.CompensatingPayment;
        sagaState.FailureReason = @event.Reason;

        // Trigger refund
        await _eventBus.PublishAsync(new Shared.Contracts.IntegrationEvents.Payment.RefundRequestedIntegrationEvent(
            @event.OrderId, @event.CorrelationId, @event.TraceId), ct);

        // Then release inventory (after refund via choreography)
        await CancelOrderAsync(@event.OrderId, @event.Reason,
            @event.CorrelationId, @event.TraceId, ct);
    }

    // ── Shared compensation helper ─────────────────────────────────────────

    private async Task CancelOrderAsync(
        Guid orderId, string reason,
        string correlationId, string traceId,
        CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(OrderId.From(orderId), ct);
        if (order is not null)
        {
            order.Cancel(reason);
            _orderRepository.Update(order);
        }

        await _eventBus.PublishAsync(
            new Shared.Contracts.IntegrationEvents.Order.OrderCancelledIntegrationEvent(
                orderId, reason, correlationId, traceId), ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}

// ── Saga State Repository interface ───────────────────────────────────────

public interface IOrderSagaStateRepository
{
    Task<OrderSagaState?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    void Add(OrderSagaState state);
    void Update(OrderSagaState state);
}
