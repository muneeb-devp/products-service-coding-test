using Products.Application.Common.Models;
using Products.Application.Products.Dtos;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// The <em>read</em> side of the Products store: projects straight to DTOs.
/// </summary>
public interface IProductReadRepository
{
    /// <summary>
    /// Returns one page of products, filtered and sorted according to
    /// <paramref name="options"/>.
    /// </summary>
    Task<PagedResult<ProductDto>> GetPagedAsync(
        ProductQueryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single product projection, or <see langword="null"/> if absent.</summary>
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
