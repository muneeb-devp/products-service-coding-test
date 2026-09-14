using Products.Domain.Products;

namespace Products.Application.Common.Abstractions;

/// <summary>
/// The filter, sort and paging parameters for a product listing.
/// </summary>
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
