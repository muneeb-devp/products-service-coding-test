using Products.Application.Common.Models;

namespace Products.Api.Contracts;

/// <summary>
/// The wire shape of a paged collection.
/// </summary>
/// <remarks>
/// Paging metadata travels in the body rather than only in headers, so a browser
/// client can render "page 2 of 7" without reading custom response headers —
/// which, for a cross-origin request, would additionally have to be allow-listed
/// through <c>Access-Control-Expose-Headers</c>.
/// </remarks>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResponse<T>
{
    /// <summary>Items on the requested page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Maximum items per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total matching items across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Total pages available at this page size.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Whether a further page exists.</summary>
    public required bool HasNextPage { get; init; }

    /// <summary>Whether a previous page exists.</summary>
    public required bool HasPreviousPage { get; init; }
}

/// <summary>
/// Maps application-layer paged results onto their wire shape.
/// </summary>
/// <remarks>
/// An extension method rather than a static factory on
/// <see cref="PagedResponse{T}"/> itself: a static member on a generic type has
/// to be called as <c>PagedResponse&lt;ProductDto&gt;.From(x)</c>, repeating a
/// type argument the compiler can infer here.
/// </remarks>
public static class PagedResponseExtensions
{
    /// <summary>Converts a paged result into its wire representation.</summary>
    public static PagedResponse<T> ToResponse<T>(this PagedResult<T> result) => new()
    {
        Items = result.Items,
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        TotalPages = result.TotalPages,
        HasNextPage = result.HasNextPage,
        HasPreviousPage = result.HasPreviousPage,
    };
}
