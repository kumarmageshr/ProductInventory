using MediatR;
using Shared.Domain.Primitives;

namespace ProductService.Application.Products.Commands.UpdateProductPrice;

public sealed record UpdateProductPriceCommand(
    Guid ProductId,
    decimal NewPrice,
    string Currency) : IRequest<Result>;

public sealed class UpdateProductPriceCommandHandler
    : IRequestHandler<UpdateProductPriceCommand, Result>
{
    private readonly IProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductPriceCommandHandler(
        IProductRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateProductPriceCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(
            ProductId.From(request.ProductId), cancellationToken);

        if (product is null)
            return Result.Failure(ProductErrors.NotFound);

        var result = product.UpdatePrice(request.NewPrice, request.Currency);
        if (result.IsFailure) return result;

        _repository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
