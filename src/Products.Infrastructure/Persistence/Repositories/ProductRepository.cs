using Microsoft.EntityFrameworkCore;
using Products.Application.Common.Abstractions;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the write-side product store.
/// </summary>
internal sealed class ProductRepository(ProductsDbContext context) : IProductRepository
{
    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        // Tracked on purpose: the caller is about to mutate this aggregate, and
        // the change tracker is what turns those mutations into an UPDATE.
        context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SkuExistsAsync(
        Sku sku,
        Guid? excludingProductId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Products.AsNoTracking().Where(p => p.Sku == sku);

        // An update keeping its own SKU must not collide with itself.
        if (excludingProductId.HasValue)
        {
            query = query.Where(p => p.Id != excludingProductId.Value);
        }

        // AnyAsync, not CountAsync: the database can stop at the first match.
        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await context.Products.AddAsync(product, cancellationToken);

    /// <inheritdoc />
    public void Remove(Product product) => context.Products.Remove(product);
}
