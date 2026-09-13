using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Application.Products.Commands.DeleteProduct;
using Products.Application.Products.Commands.UpdateProduct;
using Products.Application.Tests.Common;
using Products.Domain.Products;
using Products.Domain.Products.Events;

namespace Products.Application.Tests.Products.Commands;

public class UpdateProductCommandHandlerTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdateProductCommandHandler _sut;

    public UpdateProductCommandHandlerTests() =>
        _sut = new UpdateProductCommandHandler(
            _repository, _unitOfWork, FixedTimeProvider.Default);

    private Product GivenExistingProduct()
    {
        var product = Product.Create(
            "Old Name", "Old description.", ProductColour.Red, 10m, "SKU-1",
            FixedTimeProvider.DefaultNow.AddDays(-1));
        product.ClearDomainEvents();

        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        return product;
    }

    [Fact]
    public async Task Handle_applies_every_changed_field()
    {
        var product = GivenExistingProduct();

        var result = await _sut.Handle(
            new UpdateProductCommand(product.Id, "New Name", "New copy.", ProductColour.Blue, 25m),
            CancellationToken.None);

        result.Name.Should().Be("New Name");
        result.Description.Should().Be("New copy.");
        result.Colour.Should().Be(ProductColour.Blue);
        result.Price.Should().Be(25m);
        result.UpdatedAt.Should().Be(FixedTimeProvider.DefaultNow);
    }

    [Fact]
    public async Task Handle_raises_a_price_changed_event_when_the_price_moves()
    {
        var product = GivenExistingProduct();

        await _sut.Handle(
            new UpdateProductCommand(product.Id, "Old Name", "Old description.", ProductColour.Red, 25m),
            CancellationToken.None);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductPriceChangedDomainEvent>();
    }

    [Fact]
    public async Task Handle_raises_nothing_when_the_update_changes_nothing()
    {
        var product = GivenExistingProduct();

        await _sut.Handle(
            new UpdateProductCommand(product.Id, "Old Name", "Old description.", ProductColour.Red, 10m),
            CancellationToken.None);

        product.DomainEvents.Should().BeEmpty();
        product.UpdatedAt.Should().Be(
            FixedTimeProvider.DefaultNow.AddDays(-1), "an idempotent update is not a modification");
    }

    [Fact]
    public async Task Handle_keeps_the_existing_currency_when_none_is_supplied()
    {
        var product = GivenExistingProduct();

        var result = await _sut.Handle(
            new UpdateProductCommand(product.Id, "New Name", null, ProductColour.Red, 25m),
            CancellationToken.None);

        result.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task Handle_throws_NotFound_for_an_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var act = () => _sut.Handle(
            new UpdateProductCommand(Guid.NewGuid(), "Name", null, ProductColour.Red, 1m),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_does_not_commit_when_the_product_is_missing()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Handle(
            new UpdateProductCommand(Guid.NewGuid(), "Name", null, ProductColour.Red, 1m),
            CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class DeleteProductCommandHandlerTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeleteProductCommandHandler _sut;

    public DeleteProductCommandHandlerTests() =>
        _sut = new DeleteProductCommandHandler(
            _repository, _unitOfWork, FixedTimeProvider.Default);

    [Fact]
    public async Task Handle_removes_the_product_and_commits()
    {
        var product = Product.Create(
            "Lamp", null, ProductColour.Red, 10m, "SKU-1", FixedTimeProvider.DefaultNow);
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        await _sut.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        _repository.Received(1).Remove(product);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_raises_ProductDeleted_while_the_aggregate_is_still_loaded()
    {
        var product = Product.Create(
            "Lamp", null, ProductColour.Red, 10m, "SKU-1", FixedTimeProvider.DefaultNow);
        product.ClearDomainEvents();
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        await _sut.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductDeletedDomainEvent>();
    }

    [Fact]
    public async Task Handle_throws_NotFound_for_an_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var act = () => _sut.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
