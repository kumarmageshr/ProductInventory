using MediatR;
using ProductService.Domain.Products;
using Shared.Domain.Primitives;

namespace ProductService.Application.Products.Queries.GetProduct;

// ── Query definition ───────────────────────────────────────────────────────

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<Result<ProductDto>>;

public sealed record GetProductCatalogQuery(
    int Page,
    int PageSize,
    string? SearchTerm,
    Guid? CategoryId) : IRequest<Result<PagedResult<ProductDto>>>;

// ── Response DTOs ──────────────────────────────────────────────────────────

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string Sku,
    string Status,
    DateTime CreatedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

// ── Handlers ───────────────────────────────────────────────────────────────

public sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _repository;

    public GetProductByIdQueryHandler(IProductRepository repository) =>
        _repository = repository;

    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(
            ProductId.From(request.ProductId), cancellationToken);

        if (product is null)
            return Result.Failure<ProductDto>(ProductErrors.NotFound);

        return Result.Success(MapToDto(product));
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id.Value,
        p.Name,
        p.Description,
        p.Price.Amount,
        p.Price.Currency,
        p.CategoryId.Value,
        p.Sku.Value,
        p.Status.ToString(),
        p.CreatedAt);
}

public sealed class GetProductCatalogQueryHandler
    : IRequestHandler<GetProductCatalogQuery, Result<PagedResult<ProductDto>>>
{
    private readonly IProductRepository _repository;

    public GetProductCatalogQueryHandler(IProductRepository repository) =>
        _repository = repository;

    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetProductCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var products = await _repository.SearchAsync(
            request.SearchTerm ?? string.Empty,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = products.Select(p => new ProductDto(
            p.Id.Value, p.Name, p.Description,
            p.Price.Amount, p.Price.Currency,
            p.CategoryId.Value, p.Sku.Value,
            p.Status.ToString(), p.CreatedAt)).ToList();

        return Result.Success(new PagedResult<ProductDto>(dtos, dtos.Count, request.Page, request.PageSize));
    }
}
