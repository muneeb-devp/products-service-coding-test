using Products.Domain.Products;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// The <em>write</em> side of the Products store: loads and persists whole
/// aggregates.
/// </summary>
/// <remarks>
/// Deliberately narrow. It deals in <see cref="Product"/> aggregates rather than
/// projections, and it exposes no <c>IQueryable</c> — leaking an
/// <c>IQueryable</c> would let query composition (and therefore EF Core's
/// translation rules) bleed into the Application layer, which is exactly the
/// coupling Clean Architecture is meant to prevent.
/// <para>
/// Reads are served by <see cref="IProductReadRepository"/>. Splitting the two
/// is the practical payoff of CQRS: the write side can stay change-tracked and
/// aggregate-shaped while the read side projects straight to DTOs.
/// </para>
/// </remarks>
public interface IProductRepository
{
    /// <summary>Loads a product for modification, or <see langword="null"/> if absent.</summary>
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a product already uses this SKU.
    /// </summary>
    /// <param name="sku">The SKU to test, already normalised by the value object.</param>
    /// <param name="excludingProductId">
    /// Optional product to ignore, so an update can keep its own SKU without
    /// colliding with itself.
    /// </param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<bool> SkuExistsAsync(
        Sku sku,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stages a new product for insertion.</summary>
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>Stages a product for deletion.</summary>
    void Remove(Product product);
}
