using Products.Domain.Products;

namespace Products.Application.Products.Dtos;

/// <summary>
/// The wire representation of a product.
/// </summary>
public sealed record ProductDto
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional long-form description.</summary>
    public string? Description { get; init; }

    /// <summary>Colour, serialised by name (e.g. <c>"Red"</c>) rather than by ordinal.</summary>
    public required ProductColour Colour { get; init; }

    /// <summary>Price amount.</summary>
    public required decimal Price { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="Price"/>.</summary>
    public required string Currency { get; init; }

    /// <summary>Business identifier.</summary>
    public required string Sku { get; init; }

    /// <summary>Creation timestamp (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last-modified timestamp (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Maps an aggregate to its wire form.
    /// </summary>
    public static ProductDto FromEntity(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Colour = product.Colour,
        Price = product.Price.Amount,
        Currency = product.Price.Currency,
        Sku = product.Sku.Value,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
    };
}
