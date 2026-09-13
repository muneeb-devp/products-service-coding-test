using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Application.Products.Dtos;

namespace Products.Application.Products.Queries.GetProductById;

/// <summary>Handles <see cref="GetProductByIdQuery"/>.</summary>
public sealed class GetProductByIdQueryHandler(IProductReadRepository readRepository)
    : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    /// <inheritdoc />
    public async Task<ProductDto> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        // Throwing rather than returning null keeps the "missing means 404"
        // decision in one place (the exception handler) instead of repeating a
        // null check in every controller action.
        return product ?? throw new NotFoundException("Product", request.Id);
    }
}
