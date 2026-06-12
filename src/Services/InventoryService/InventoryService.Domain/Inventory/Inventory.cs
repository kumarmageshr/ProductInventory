using Shared.Domain.Primitives;

namespace InventoryService.Domain.Inventory;

/// <summary>
/// Inventory Aggregate Root — manages stock for a given product.
/// Enforces stock reservation invariants.
/// </summary>
public sealed class Inventory : AggregateRoot<InventoryId>
{
    private Inventory() { }

    private Inventory(InventoryId id, ProductId productId, int quantityOnHand)
    {
        Id = id;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        QuantityReserved = 0;
        LastUpdated = DateTime.UtcNow;
    }

    public ProductId ProductId { get; private set; } = default!;
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public DateTime LastUpdated { get; private set; }

    public static Inventory Create(Guid productId, int initialStock)
    {
        if (initialStock < 0)
            throw new ArgumentException("Initial stock cannot be negative.", nameof(initialStock));

        var inventory = new Inventory(
            InventoryId.New(),
            new ProductId(productId),
            initialStock);

        inventory.RaiseDomainEvent(new InventoryCreatedDomainEvent(
            inventory.Id.Value, productId, initialStock));

        return inventory;
    }

    // ── Business Operations ────────────────────────────────────────────────

    public Result Reserve(Guid orderId, int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(InventoryErrors.InvalidQuantity);

        if (QuantityAvailable < quantity)
            return Result.Failure(InventoryErrors.InsufficientStock);

        QuantityReserved += quantity;
        LastUpdated = DateTime.UtcNow;

        RaiseDomainEvent(new StockReservedDomainEvent(
            Id.Value, ProductId.Value, orderId, quantity, QuantityAvailable));

        return Result.Success();
    }

    public Result Release(Guid orderId, int quantity)
    {
        if (quantity <= 0)
            return Result.Failure(InventoryErrors.InvalidQuantity);

        if (QuantityReserved < quantity)
            return Result.Failure(InventoryErrors.CannotReleaseMoreThanReserved);

        QuantityReserved -= quantity;
        LastUpdated = DateTime.UtcNow;

        RaiseDomainEvent(new StockReleasedDomainEvent(
            Id.Value, ProductId.Value, orderId, quantity));

        return Result.Success();
    }

    public Result Adjust(int adjustment, string reason)
    {
        var newQuantity = QuantityOnHand + adjustment;
        if (newQuantity < 0)
            return Result.Failure(InventoryErrors.AdjustmentWouldResultInNegativeStock);

        var previousQuantity = QuantityOnHand;
        QuantityOnHand = newQuantity;
        LastUpdated = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryAdjustedDomainEvent(
            Id.Value, ProductId.Value, previousQuantity, QuantityOnHand, adjustment, reason));

        return Result.Success();
    }

    public Result Commit(int quantity)
    {
        // Called when order is confirmed — removes from reserved and on-hand
        if (QuantityReserved < quantity)
            return Result.Failure(InventoryErrors.CannotReleaseMoreThanReserved);

        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
        LastUpdated = DateTime.UtcNow;
        return Result.Success();
    }
}

public record InventoryId(Guid Value)
{
    public static InventoryId New() => new(Guid.NewGuid());
}

public record ProductId(Guid Value);
