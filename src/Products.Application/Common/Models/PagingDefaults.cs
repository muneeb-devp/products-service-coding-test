namespace Products.Application.Common.Models;

/// <summary>
/// Default and limit values for paged endpoints.
/// </summary>
public static class PagingDefaults
{
    /// <summary>Page returned when the caller does not ask for one.</summary>
    public const int Page = 1;

    /// <summary>Page size used when the caller does not ask for one.</summary>
    public const int PageSize = 20;

    /// <summary>
    /// Hard ceiling on page size.
    /// </summary>
    public const int MaxPageSize = 100;
}
