namespace Products.Application.Common.Models;

/// <summary>
/// Default and limit values for paged endpoints.
/// </summary>
/// <remarks>
/// These live in their own type rather than on the query record because C# will
/// not resolve a type's own constants inside its primary-constructor parameter
/// defaults. Hoisting them here keeps one definition rather than a literal in
/// the signature and a constant in the validator that can silently disagree.
/// </remarks>
public static class PagingDefaults
{
    /// <summary>Page returned when the caller does not ask for one.</summary>
    public const int Page = 1;

    /// <summary>Page size used when the caller does not ask for one.</summary>
    public const int PageSize = 20;

    /// <summary>
    /// Hard ceiling on page size.
    /// </summary>
    /// <remarks>
    /// Without a cap, <c>?pageSize=1000000</c> is a denial-of-service vector:
    /// one request that materialises the whole table into memory and serialises
    /// it. The validator rejects anything above this.
    /// </remarks>
    public const int MaxPageSize = 100;
}
