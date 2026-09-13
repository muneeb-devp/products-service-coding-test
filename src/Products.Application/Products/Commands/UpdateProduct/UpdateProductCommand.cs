using MediatR;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Application.Products.Commands.UpdateProduct;

/// <summary>
/// Replaces the mutable attributes of an existing product.
/// </summary>
/// <param name="Id">Identifier of the product to update.</param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description, or <see langword="null"/> to clear it.</param>
/// <param name="Colour">New colour.</param>
/// <param name="Price">New price amount.</param>
/// <param name="Currency">ISO 4217 code; keeps the existing currency when omitted.</param>
public sealed record UpdateProductCommand(
    Guid Id,
    string? Name,
    string? Description,
    ProductColour Colour,
    decimal Price,
    string? Currency = null) : IRequest<ProductDto>;
