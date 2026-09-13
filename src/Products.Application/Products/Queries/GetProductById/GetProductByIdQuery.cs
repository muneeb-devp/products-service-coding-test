using MediatR;
using Products.Application.Products.Dtos;

namespace Products.Application.Products.Queries.GetProductById;

/// <summary>Returns a single product by its identifier.</summary>
/// <param name="Id">The product identifier.</param>
public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
