using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;

namespace Products.Application.Products.Queries.GetProducts;

/// <summary>
/// Handles <see cref="GetProductsQuery"/> by delegating to the read side.
/// </summary>
public sealed class GetProductsQueryHandler(IProductReadRepository readRepository)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    /// <inheritdoc />
    public Task<PagedResult<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken) =>
        readRepository.GetPagedAsync(request.ToOptions(), cancellationToken);
}
