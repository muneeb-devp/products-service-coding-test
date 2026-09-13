using MediatR;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;

namespace Products.Application.Products.Commands.DeleteProduct;

/// <summary>Handles <see cref="DeleteProductCommand"/>.</summary>
public sealed class DeleteProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteProductCommand>
{
    /// <inheritdoc />
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        // Raise the event before removing, while the aggregate is still loaded
        // and can describe what is being deleted.
        product.MarkDeleted(timeProvider.GetUtcNow());
        repository.Remove(product);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
