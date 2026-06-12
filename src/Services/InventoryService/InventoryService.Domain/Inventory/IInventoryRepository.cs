using Shared.Domain.Primitives;

namespace InventoryService.Domain.Inventory;

public interface IInventoryRepository : IRepository<Inventory, InventoryId>
{
    Task<Inventory?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inventory>> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default);
}
