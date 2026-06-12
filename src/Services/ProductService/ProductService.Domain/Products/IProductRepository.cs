using Shared.Domain.Primitives;

namespace ProductService.Domain.Products;

/// <summary>
/// Repository interface — defined in the Domain, implemented in Infrastructure.
/// Follows Dependency Inversion Principle.
/// </summary>
public interface IProductRepository : IRepository<Product, ProductId>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(CategoryId categoryId, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, int page, int pageSize, CancellationToken cancellationToken = default);
}
