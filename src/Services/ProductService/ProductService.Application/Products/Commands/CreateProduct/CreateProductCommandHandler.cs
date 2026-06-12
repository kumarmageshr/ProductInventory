using MediatR;
using ProductService.Domain.Products;
using Shared.Domain.Primitives;

namespace ProductService.Application.Products.Commands.CreateProduct;

/// <summary>
/// MediatR Command Handler — orchestrates domain logic and persistence.
/// Single Responsibility: one handler per command.
/// </summary>
public sealed class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        // Guard: SKU uniqueness (domain invariant enforced at application boundary)
        if (await _productRepository.SkuExistsAsync(request.Sku, cancellationToken))
            return Result.Failure<Guid>(ProductErrors.SkuAlreadyExists);

        var productResult = Product.Create(
            request.Name,
            request.Description,
            request.Price,
            request.Currency,
            request.CategoryId,
            request.Sku);

        if (productResult.IsFailure)
            return Result.Failure<Guid>(productResult.Error);

        _productRepository.Add(productResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // Domain event ProductCreatedDomainEvent is captured by DomainEventToOutboxInterceptor

        return Result.Success(productResult.Value.Id.Value);
    }
}
