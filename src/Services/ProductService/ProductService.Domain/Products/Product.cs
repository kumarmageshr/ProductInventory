using Shared.Domain.Primitives;

namespace ProductService.Domain.Products;

/// <summary>
/// Product Aggregate Root — consistency boundary for product data.
/// Encapsulates all business rules for product lifecycle.
/// </summary>
public sealed class Product : AggregateRoot<ProductId>
{
    private Product() { } // EF Core

    private Product(
        ProductId id,
        string name,
        string description,
        Money price,
        CategoryId categoryId,
        Sku sku)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        CategoryId = categoryId;
        Sku = sku;
        Status = ProductStatus.Active;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money Price { get; private set; } = default!;
    public CategoryId CategoryId { get; private set; } = default!;
    public Sku Sku { get; private set; } = default!;
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }

    // ── Factory ────────────────────────────────────────────────────────────

    public static Result<Product> Create(
        string name,
        string description,
        decimal price,
        string currency,
        Guid categoryId,
        string sku)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(ProductErrors.NameRequired);

        if (price <= 0)
            return Result.Failure<Product>(ProductErrors.InvalidPrice);

        var product = new Product(
            ProductId.New(),
            name.Trim(),
            description.Trim(),
            new Money(price, currency),
            new CategoryId(categoryId),
            new Sku(sku));

        product.RaiseDomainEvent(new ProductCreatedDomainEvent(product.Id.Value, product.Name));

        return product;
    }

    // ── Business Operations ────────────────────────────────────────────────

    public Result UpdatePrice(decimal newPrice, string currency)
    {
        if (newPrice <= 0)
            return Result.Failure(ProductErrors.InvalidPrice);

        var oldPrice = Price;
        Price = new Money(newPrice, currency);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ProductPriceChangedDomainEvent(Id.Value, oldPrice, Price));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure(ProductErrors.AlreadyDiscontinued);

        Status = ProductStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProductDeactivatedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status == ProductStatus.Discontinued)
            return Result.Failure(ProductErrors.AlreadyDiscontinued);

        Status = ProductStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result UpdateDetails(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(ProductErrors.NameRequired);

        Name = name.Trim();
        Description = description.Trim();
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }
}

public enum ProductStatus { Active, Inactive, Discontinued }
