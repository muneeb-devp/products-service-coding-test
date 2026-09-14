using Microsoft.EntityFrameworkCore;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the read-side product store.
/// </summary>
internal sealed class ProductReadRepository(ProductsDbContext context) : IProductReadRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<ProductDto>> GetPagedAsync(
        ProductQueryOptions options,
        CancellationToken cancellationToken = default)
    {
        var query = context.Products.AsNoTracking();

        // The colour filter is optional. When absent this is simply "all
        // products", one code path, one set of SQL, whether or not a filter
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

            // "createdat", plus the fallback for anything the validator let
            // through unrecognised. Direction stays under the caller's control
            // via sortDescending, so this picks the field only.
            _ => descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };

        return sorted.ThenBy(p => p.Id);
    }
}
