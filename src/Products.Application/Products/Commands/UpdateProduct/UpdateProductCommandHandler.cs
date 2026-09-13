using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Application.Products.Dtos;

namespace Products.Application.Products.Commands.UpdateProduct;

/// <summary>Handles <see cref="UpdateProductCommand"/>.</summary>
public sealed class UpdateProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProductCommand, ProductDto>
{
    /// <inheritdoc />
    public async Task<ProductDto> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        var now = timeProvider.GetUtcNow();

        // Two calls because they are two different business operations with
        // different consequences: a price change raises a domain event that
        // downstream services care about, a rename does not.
        product.UpdateDetails(request.Name, request.Description, request.Colour, now);
        product.ChangePrice(request.Price, now, request.Currency);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductDto.FromEntity(product);
    }
}
