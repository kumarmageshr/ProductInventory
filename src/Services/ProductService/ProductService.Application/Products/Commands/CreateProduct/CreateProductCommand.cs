using MediatR;
using Shared.Domain.Primitives;

namespace ProductService.Application.Products.Commands.CreateProduct;

/// <summary>
/// CQRS Command — intent to create a new product.
/// Commands are write-side; they mutate state.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string Sku) : IRequest<Result<Guid>>;
