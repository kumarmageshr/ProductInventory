using MediatR;
using InventoryService.Domain.Inventory;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Messaging;
using Shared.Contracts.IntegrationEvents.Inventory;
using Shared.Contracts.IntegrationEvents.Order;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace InventoryService.Application.Inventory.EventHandlers;

/// <summary>
/// Integration Event Handler — subscribes to OrderCreated topic.
/// When an order is created, reserves inventory for all items.
/// Publishes InventoryReserved or InventoryReservationFailed back to the bus.
/// This is the Choreography-based Saga participant.
/// </summary>
public sealed class OrderCreatedIntegrationEventHandler
    : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private readonly IInventoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderCreatedIntegrationEventHandler> _logger;

    public OrderCreatedIntegrationEventHandler(
        IInventoryRepository repository,
        IUnitOfWork unitOfWork,
        IEventBus eventBus,
        ILogger<OrderCreatedIntegrationEventHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(
        OrderCreatedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current;
        _logger.LogInformation(
            "Handling OrderCreated for Order {OrderId}", @event.OrderId);

        var reservedItems = new List<ReservedItemDto>();

        foreach (var item in @event.Items)
        {
            var inventory = await _repository.GetByProductIdAsync(
                item.ProductId, cancellationToken);

            if (inventory is null)
            {
                await PublishFailureAsync(
                    @event, $"Product {item.ProductId} not found in inventory.", cancellationToken);
                return;
            }

            var result = inventory.Reserve(@event.OrderId, item.Quantity);
            if (result.IsFailure)
            {
                await PublishFailureAsync(@event, result.Error.Description, cancellationToken);
                return;
            }

            _repository.Update(inventory);
            reservedItems.Add(new ReservedItemDto(item.ProductId, item.Quantity));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new InventoryReservedIntegrationEvent(
            @event.OrderId, reservedItems, @event.CorrelationId, @event.TraceId),
            cancellationToken);

        _logger.LogInformation("Inventory reserved for Order {OrderId}", @event.OrderId);
    }

    private async Task PublishFailureAsync(
        OrderCreatedIntegrationEvent @event,
        string reason,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Inventory reservation failed for Order {OrderId}: {Reason}",
            @event.OrderId, reason);

        await _eventBus.PublishAsync(new InventoryReservationFailedIntegrationEvent(
            @event.OrderId, reason, @event.CorrelationId, @event.TraceId), ct);
    }
}
