using MediatR;
using OrderService.Application.Orders.Sagas;
using OrderService.Domain.Orders;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Messaging;
using System.Security.Claims;

namespace OrderService.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    List<CreateOrderItemDto> Items,
    string Currency,
    string CorrelationId,
    string TraceId) : IRequest<Result<Guid>>;

public sealed record CreateOrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OrderSagaOrchestrator _saga;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        OrderSagaOrchestrator saga)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _saga = saga;
    }

    public async Task<Result<Guid>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        var items = request.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
            .ToList();

        var orderResult = Order.Create(request.CustomerId, items, request.Currency);
        if (orderResult.IsFailure)
            return Result.Failure<Guid>(orderResult.Error);

        var order = orderResult.Value;
        _orderRepository.Add(order);

        // Save order first, then start saga (outbox will capture domain events)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Start the Saga — triggers the workflow asynchronously
        await _saga.StartAsync(order, request.CorrelationId, request.TraceId, cancellationToken);

        return Result.Success(order.Id.Value);
    }
}
