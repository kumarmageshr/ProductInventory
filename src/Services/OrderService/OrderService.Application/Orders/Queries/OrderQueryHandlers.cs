using MediatR;
using OrderService.Domain.Orders;
using Shared.Domain.Primitives;

namespace OrderService.Application.Orders.Queries;

// ── Order Status ───────────────────────────────────────────────────────────

public sealed record GetOrderStatusQuery(Guid OrderId, Guid CustomerId)
    : IRequest<Result<OrderStatusDto>>;

public sealed record OrderStatusDto(
    Guid OrderId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    string? CancellationReason);

public sealed class GetOrderStatusQueryHandler
    : IRequestHandler<GetOrderStatusQuery, Result<OrderStatusDto>>
{
    private readonly IOrderRepository _repository;

    public GetOrderStatusQueryHandler(IOrderRepository repository) =>
        _repository = repository;

    public async Task<Result<OrderStatusDto>> Handle(
        GetOrderStatusQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(
            OrderId.From(request.OrderId), cancellationToken);

        if (order is null)
            return Result.Failure<OrderStatusDto>(OrderErrors.NotFound);

        // Authorization: customers can only see their own orders
        if (order.CustomerId.Value != request.CustomerId)
            return Result.Failure<OrderStatusDto>(OrderErrors.Unauthorized);

        return Result.Success(new OrderStatusDto(
            order.Id.Value,
            order.Status.ToString(),
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            order.CreatedAt,
            order.CompletedAt,
            order.CancelledAt,
            order.CancellationReason));
    }
}

// ── Order History ──────────────────────────────────────────────────────────

public sealed record GetOrderHistoryQuery(
    Guid CustomerId, int Page, int PageSize) : IRequest<Result<List<OrderSummaryDto>>>;

public sealed record OrderSummaryDto(
    Guid OrderId,
    string Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    DateTime CreatedAt);

public sealed class GetOrderHistoryQueryHandler
    : IRequestHandler<GetOrderHistoryQuery, Result<List<OrderSummaryDto>>>
{
    private readonly IOrderRepository _repository;

    public GetOrderHistoryQueryHandler(IOrderRepository repository) =>
        _repository = repository;

    public async Task<Result<List<OrderSummaryDto>>> Handle(
        GetOrderHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var orders = await _repository.GetOrderHistoryAsync(
            request.CustomerId, request.Page, request.PageSize, cancellationToken);

        var dtos = orders.Select(o => new OrderSummaryDto(
            o.Id.Value,
            o.Status.ToString(),
            o.TotalAmount.Amount,
            o.TotalAmount.Currency,
            o.Items.Count,
            o.CreatedAt)).ToList();

        return Result.Success(dtos);
    }
}
