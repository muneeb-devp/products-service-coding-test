using Microsoft.EntityFrameworkCore;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the read-side product store.
/// </summary>
/// <remarks>
/// Every query here is <c>AsNoTracking</c>. Read requests never mutate
/// anything, so paying for change-tracking snapshots on each row is wasted work
/// and wasted memory on the hot path.
/// </remarks>
internal sealed class ProductReadRepository(ProductsDbContext context) : IProductReadRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<ProductDto>> GetPagedAsync(
        ProductQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var query = context.Products.AsNoTracking();

        // The colour filter is optional. When absent this is simply "all
        // products" — one code path, one set of SQL, whether or not a filter
        // was supplied.
        if (options.Colour.HasValue)
        {
            var colour = options.Colour.Value;
            query = query.Where(p => p.Colour == colour);
        }

        // COUNT runs against the filtered query but before paging, so it
        // reports how many rows match overall rather than how many are on this
        // page.
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            // An empty result set is a valid answer, not an error. Skip the
            // second round trip and return 200 with an empty page.
            return new PagedResult<ProductDto>
            {
                Items = [],
                Page = options.Page,
                PageSize = options.PageSize,
                TotalCount = 0,
            };
        }

        query = ApplySort(query, options);

        var products = await query
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>
        {
            Items = products.Select(ProductDto.FromEntity).ToArray(),
            Page = options.Page,
            PageSize = options.PageSize,
            TotalCount = totalCount,
        };
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null ? null : ProductDto.FromEntity(product);
    }

    /// <summary>
    /// Translates the validated sort field onto a typed expression.
    /// </summary>
    /// <remarks>
    /// A switch over a closed set, not string-to-column concatenation. The sort
    /// field arrives from the query string; the validator already restricts it
    /// to an allow-list, and this switch means an unrecognised value falls back
    /// to the default ordering rather than reaching the database as text.
    /// <para>
    /// The secondary sort on <c>Id</c> is not decoration. Paging over a
    /// non-unique sort key (several products sharing a price, say) has no
    /// defined row order between pages, so the same row can appear on page 1 and
    /// again on page 2 while another is skipped entirely. Appending a unique
    /// tiebreaker makes the ordering total and the paging stable.
    /// </para>
    /// </remarks>
    private static IQueryable<Product> ApplySort(
        IQueryable<Product> query,
        ProductQueryOptions options)
    {
        var descending = options.SortDescending;

        var sorted = options.SortBy.ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),

            "price" => descending
                ? query.OrderByDescending(p => p.Price.Amount)
                : query.OrderBy(p => p.Price.Amount),

            "colour" or "color" => descending
                ? query.OrderByDescending(p => p.Colour)
                : query.OrderBy(p => p.Colour),

            "sku" => descending
                ? query.OrderByDescending(p => p.Sku)
                : query.OrderBy(p => p.Sku),

            "updatedat" => descending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),

            // "createdat" and anything unrecognised: newest first is the most
            // useful default for a catalogue listing.
            _ => descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };

        return sorted.ThenBy(p => p.Id);
    }
}
