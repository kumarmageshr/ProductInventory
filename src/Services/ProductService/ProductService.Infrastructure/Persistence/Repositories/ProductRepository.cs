using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Products;
using Shared.Domain.Primitives;

namespace ProductService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository Pattern implementation for Product aggregate.
/// All EF Core queries isolated here — never leaking into application layer.
/// </summary>
internal sealed class ProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public ProductRepository(ProductDbContext context) => _context = context;

    public async Task<Product?> GetByIdAsync(
        ProductId id, CancellationToken cancellationToken = default) =>
        await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Product?> GetBySkuAsync(
        string sku, CancellationToken cancellationToken = default) =>
        await _context.Products
            .FirstOrDefaultAsync(p => p.Sku.Value == sku, cancellationToken);

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        CategoryId categoryId, CancellationToken cancellationToken = default) =>
        await _context.Products
            .Where(p => p.CategoryId == categoryId && p.Status == ProductStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> SkuExistsAsync(
        string sku, CancellationToken cancellationToken = default) =>
        await _context.Products.AnyAsync(p => p.Sku.Value == sku, cancellationToken);

    public async Task<IReadOnlyList<Product>> SearchAsync(
        string searchTerm, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.Where(p => p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(p =>
                p.Name.Contains(searchTerm) ||
                p.Description.Contains(searchTerm));

        return await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public void Add(Product aggregate) => _context.Products.Add(aggregate);
    public void Update(Product aggregate) => _context.Products.Update(aggregate);
    public void Remove(Product aggregate) => _context.Products.Remove(aggregate);
}
