using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Application.Products.Dtos;
using Products.Domain.Products;

namespace Products.Application.Products.Commands.CreateProduct;

/// <summary>
/// Handles <see cref="CreateProductCommand"/>.
/// </summary>
public sealed class CreateProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    /// <inheritdoc />
    public async Task<ProductDto> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        // Normalise first: the value object decides what "the same SKU" means,
        // so the uniqueness check must run against the normalised form.
        var sku = Sku.Create(request.Sku);

        if (await repository.SkuExistsAsync(sku, cancellationToken: cancellationToken))
        {
            // A pre-check, not a guarantee. Two concurrent requests can both pass
            // it; the unique index on Sku is the actual enforcement, and the
            // infrastructure layer translates that violation into this same
            // exception. This check exists to give the common case a clean 409
            // instead of a database error.
            throw new ConflictException($"A product with SKU '{sku.Value}' already exists.");
        }

        var product = Product.Create(
            request.Name,
            request.Description,
            request.Colour,
            request.Price,
            request.Sku,
            timeProvider.GetUtcNow(),
            request.Currency);

        await repository.AddAsync(product, cancellationToken);

        // Committing here also dispatches ProductCreatedDomainEvent, after the
        // transaction succeeds, never before.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductDto.FromEntity(product);
    }
}
