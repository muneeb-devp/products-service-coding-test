using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;

namespace Products.Application.Products.Queries.GetProducts;

/// <summary>
/// Handles <see cref="GetProductsQuery"/> by delegating to the read side.
/// </summary>
/// <remarks>
/// Filtering, sorting and paging all happen in the database. Materialising the
/// table and then calling <c>.Where()</c> in memory is the standard way this
/// endpoint becomes the slowest thing in the system once the catalogue grows.
/// </remarks>
public sealed class GetProductsQueryHandler(IProductReadRepository readRepository)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    /// <inheritdoc />
    public Task<PagedResult<ProductDto>> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken) =>
        readRepository.GetPagedAsync(request.ToOptions(), cancellationToken);
}
