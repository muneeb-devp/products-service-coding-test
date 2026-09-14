using Products.Application.Products.Commands.CreateProduct;
using Products.Domain.Products;

namespace Products.Api.Contracts;

/// <summary>
/// Request body for creating a product.
/// </summary>
/// <remarks>
/// A transport-layer type distinct from <see cref="CreateProductCommand"/>. It
/// looks like duplication at this size, and it earns its place the first time
/// the HTTP contract and the internal command need to diverge — a renamed JSON
/// field, a deprecated property kept for older clients — without either change
/// forcing the other.
/// </remarks>
/// <param name="Name">Display name. Required, 200 characters or fewer.</param>
/// <param name="Description">Optional description, 2000 characters or fewer.</param>
/// <param name="Colour">Colour name, e.g. <c>"Red"</c>. Case-insensitive.</param>
/// <param name="Price">Price. Zero or greater, at most two decimal places.</param>
/// <param name="Sku">Business identifier. Unique; letters, digits and hyphens.</param>
/// <param name="Currency">Optional ISO 4217 code. Defaults to <c>GBP</c>.</param>
public sealed record CreateProductRequest(
    string? Name,
    string? Description,
    ProductColour Colour,
    decimal Price,
    string? Sku,
    string? Currency = null)
{
    /// <summary>Maps the request onto its application command.</summary>
    public CreateProductCommand ToCommand() =>
        new(Name, Description, Colour, Price, Sku, Currency);
}
