using MediatR;

namespace Products.Application.Products.Commands.DeleteProduct;

/// <summary>Removes a product from the catalogue.</summary>
/// <param name="Id">Identifier of the product to remove.</param>
public sealed record DeleteProductCommand(Guid Id) : IRequest;
