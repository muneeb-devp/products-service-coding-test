using MediatR;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Application.Products.Commands.CreateProduct;

/// <summary>
/// Creates a new product in the catalogue.
/// </summary>
/// <param name="Name">Display name. Required.</param>
/// <param name="Description">Optional long-form description.</param>
/// <param name="Colour">Colour; must be one of the defined values.</param>
/// <param name="Price">Price amount. Must be zero or greater.</param>
/// <param name="Sku">Business identifier. Must be unique across the catalogue.</param>
/// <param name="Currency">ISO 4217 code. Defaults to GBP when omitted.</param>
public sealed record CreateProductCommand(
    string? Name,
    string? Description,
    ProductColour Colour,
    decimal Price,
    string? Sku,
    string? Currency = null) : IRequest<ProductDto>;
