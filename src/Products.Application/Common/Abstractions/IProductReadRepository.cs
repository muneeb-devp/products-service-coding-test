using Products.Application.Common.Models;
using Products.Application.Products.Dtos;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// The <em>read</em> side of the Products store: projects straight to DTOs.
/// </summary>
/// <remarks>
/// Returning DTOs rather than entities is what makes this worth separating.
/// The implementation can project in the database (<c>SELECT</c> only the
/// columns the DTO needs) and skip EF's change tracker entirely, neither of
/// which is available if the read path has to hydrate full aggregates first.
/// </remarks>
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
