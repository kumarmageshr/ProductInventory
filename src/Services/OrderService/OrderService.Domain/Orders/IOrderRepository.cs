using Shared.Domain.Primitives;

namespace OrderService.Domain.Orders;

public interface IOrderRepository : IRepository<Order, OrderId>
{
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetOrderHistoryAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken = default);
}
