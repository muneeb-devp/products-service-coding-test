using Products.Domain.Products;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// The filter, sort and paging parameters for a product listing.
/// </summary>
/// <remarks>
/// A single options object, with the colour filter <em>optional</em>, is what
/// lets "list all products" and "list only the red ones" share one query handler
/// and one SQL path. Giving the colour filter its own endpoint and its own
/// handler would duplicate the paging and sorting logic, and the two copies
/// would drift.
/// </remarks>
public sealed record ProductQueryOptions
{
    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Items per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Optional colour filter. <see langword="null"/> means "any colour".</summary>
    public ProductColour? Colour { get; init; }

    /// <summary>Field to sort by. Validated against <see cref="AllowedSortFields"/>.</summary>
    public string SortBy { get; init; } = DefaultSortField;

    /// <summary>Whether the sort runs in descending order.</summary>
    public bool SortDescending { get; init; }

    /// <summary>Field used when the caller does not specify one.</summary>
    public const string DefaultSortField = "createdAt";

    /// <summary>
    /// The sort fields a client may request.
    /// </summary>
    /// <remarks>
    /// An allow-list, not a free-text column name. The value arrives from the
    /// query string, and mapping it to a column by string concatenation would be
    /// an injection vector; matching it against this closed set removes the
    /// question entirely. Comparison is case-insensitive so <c>createdat</c> and
    /// <c>createdAt</c> both work.
    /// </remarks>
    public static readonly IReadOnlySet<string> AllowedSortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "price",
            "colour",
            "color",
            "sku",
            "createdAt",
            "updatedAt",
        };
}
