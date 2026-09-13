namespace Products.Application.Common.Models;

/// <summary>
/// One page of results plus the metadata a client needs to page through the rest.
/// </summary>
/// <remarks>
/// Returning a bare array from a collection endpoint is a latent production
/// incident: it works on the developer's 20 rows and falls over on the
/// customer's 2 million. Paging is the default here, not an opt-in.
/// </remarks>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>The items on this page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>The 1-based page number that was returned.</summary>
    public required int Page { get; init; }

    /// <summary>The maximum number of items per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total number of items matching the query across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Total number of pages available for the current page size.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Whether a page exists after this one.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a page exists before this one.</summary>
    public bool HasPreviousPage => Page > 1;
}
