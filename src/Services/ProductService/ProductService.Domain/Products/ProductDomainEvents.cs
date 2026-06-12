using Shared.Domain.Primitives;

namespace ProductService.Domain.Products;

// ── Domain Events ──────────────────────────────────────────────────────────

public sealed record ProductCreatedDomainEvent(
    Guid ProductId,
    string Name) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    Money OldPrice,
    Money NewPrice) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record ProductDeactivatedDomainEvent(Guid ProductId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Domain Errors ──────────────────────────────────────────────────────────

public static class ProductErrors
{
    public static readonly Error NameRequired =
        new("Product.NameRequired", "Product name is required.");

    public static readonly Error InvalidPrice =
        new("Product.InvalidPrice", "Price must be greater than zero.");

    public static readonly Error AlreadyDiscontinued =
        new("Product.AlreadyDiscontinued", "Product is already discontinued.");

    public static readonly Error NotFound =
        new("Product.NotFound", "Product was not found.");

    public static readonly Error SkuAlreadyExists =
        new("Product.SkuAlreadyExists", "A product with this SKU already exists.");
}
