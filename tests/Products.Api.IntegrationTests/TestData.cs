using System.Net.Http.Json;

namespace Products.Api.IntegrationTests;

/// <summary>Helpers for creating products through the public API during tests.</summary>
internal static class TestData
{
    /// <summary>A valid create-product payload, overridable per test.</summary>
    public static object CreateProductPayload(
        string name = "Ergonomic Desk Lamp",
        string? description = "A lamp.",
        string colour = "Red",
        decimal price = 49.99m,
        string sku = "LAMP-001",
        string? currency = null) =>
        new { name, description, colour, price, sku, currency };

    /// <summary>
    /// Creates a product through the real endpoint and returns the response body.
    /// </summary>
    /// <remarks>
    /// Arranged through HTTP rather than by writing to the DbContext directly, so
    /// the fixture data goes through exactly the validation and normalisation a
    /// real client's data would.
    /// </remarks>
    public static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        string name = "Ergonomic Desk Lamp",
        string colour = "Red",
        decimal price = 49.99m,
        string sku = "LAMP-001")
    {
        var response = await client.PostAsJsonAsync(
            "/api/products",
            CreateProductPayload(name: name, colour: colour, price: price, sku: sku));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }
}

/// <summary>Mirror of the API's product response, for deserialising in tests.</summary>
internal sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    string Colour,
    decimal Price,
    string Currency,
    string Sku,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Mirror of the API's paged response envelope.</summary>
internal sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
