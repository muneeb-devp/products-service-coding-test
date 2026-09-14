using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Application.Products.Queries.GetProducts;

/// <summary>
/// Returns a page of products, optionally filtered by colour.
/// </summary>
/// <param name="Page">1-based page number. Defaults to 1.</param>
/// <param name="PageSize">Items per page. Defaults to 20, capped at 100.</param>
/// <param name="Colour">Optional colour filter; <see langword="null"/> returns every colour.</param>
/// <param name="SortBy">Field to sort by; must be in the allow-list.</param>
/// <param name="SortDescending">Whether to sort descending.</param>
public sealed record GetProductsQuery(
    int Page = PagingDefaults.Page,
    int PageSize = PagingDefaults.PageSize,
    ProductColour? Colour = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PagedResult<ProductDto>>
{
    /// <summary>Projects the query onto the options the read repository consumes.</summary>
    public ProductQueryOptions ToOptions() => new()
    {
        Page = Page,
        PageSize = PageSize,
        Colour = Colour,
        SortBy = string.IsNullOrWhiteSpace(SortBy) ? ProductQueryOptions.DefaultSortField : SortBy,
        SortDescending = SortDescending,
    };
}
