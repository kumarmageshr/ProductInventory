using Shared.Domain.Primitives;

namespace InventoryService.Domain.Inventory;

// ── Domain Events ──────────────────────────────────────────────────────────

public sealed record InventoryCreatedDomainEvent(
    Guid InventoryId, Guid ProductId, int InitialStock) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record StockReservedDomainEvent(
    Guid InventoryId, Guid ProductId, Guid OrderId,
    int QuantityReserved, int QuantityAvailable) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record StockReleasedDomainEvent(
    Guid InventoryId, Guid ProductId, Guid OrderId, int QuantityReleased) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record InventoryAdjustedDomainEvent(
    Guid InventoryId, Guid ProductId,
    int PreviousQuantity, int NewQuantity, int Adjustment, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Domain Errors ──────────────────────────────────────────────────────────

public static class InventoryErrors
{
    public static readonly Error InvalidQuantity =
        new("Inventory.InvalidQuantity", "Quantity must be greater than zero.");

    public static readonly Error InsufficientStock =
        new("Inventory.InsufficientStock", "Insufficient stock available.");

    public static readonly Error CannotReleaseMoreThanReserved =
        new("Inventory.CannotRelease", "Cannot release more than reserved quantity.");

    public static readonly Error AdjustmentWouldResultInNegativeStock =
        new("Inventory.NegativeStock", "Adjustment would result in negative stock.");

    public static readonly Error NotFound =
        new("Inventory.NotFound", "Inventory record not found for product.");
}
