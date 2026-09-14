using Products.Application.Products.Commands.UpdateProduct;
using Products.Domain.Products;

namespace Products.Api.Contracts;

/// <summary>Request body for replacing a product's mutable attributes.</summary>
/// <param name="Name">Display name. Required.</param>
/// <param name="Description">Optional description; omit or null to clear it.</param>
/// <param name="Colour">Colour name, e.g. <c>"Blue"</c>.</param>
/// <param name="Price">Price. Zero or greater.</param>
/// <param name="Currency">Optional ISO 4217 code; keeps the existing one when omitted.</param>
public sealed record UpdateProductRequest(
    string? Name,
    string? Description,
    ProductColour Colour,
    decimal Price,
    string? Currency = null)
{
    /// <summary>Maps the request and route id onto the application command.</summary>
    public UpdateProductCommand ToCommand(Guid id) =>
        new(id, Name, Description, Colour, Price, Currency);
}
