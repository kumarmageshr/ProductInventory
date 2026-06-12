using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Products;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence.Interceptors;

namespace ProductService.Infrastructure.Persistence;

/// <summary>
/// Product Service DbContext.
/// Each microservice owns its own DbContext and database — Database Per Service pattern.
/// </summary>
public sealed class ProductDbContext : DbContext, IUnitOfWork
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // IUnitOfWork implementation delegates to EF Core SaveChangesAsync
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
