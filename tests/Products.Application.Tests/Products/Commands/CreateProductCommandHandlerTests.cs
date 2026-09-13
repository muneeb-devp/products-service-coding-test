using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Application.Products.Commands.CreateProduct;
using Products.Application.Tests.Common;
using Products.Domain.Products;

namespace Products.Application.Tests.Products.Commands;

public class CreateProductCommandHandlerTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateProductCommandHandler _sut;

    public CreateProductCommandHandlerTests() =>
        _sut = new CreateProductCommandHandler(
            _repository, _unitOfWork, FixedTimeProvider.Default);

    private static CreateProductCommand ValidCommand => new(
        Name: "Ergonomic Desk Lamp",
        Description: "A lamp.",
        Colour: ProductColour.Red,
        Price: 49.99m,
        Sku: "lamp-001");

    [Fact]
    public async Task Handle_returns_a_dto_describing_the_created_product()
    {
        var result = await _sut.Handle(ValidCommand, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Ergonomic Desk Lamp");
        result.Colour.Should().Be(ProductColour.Red);
        result.Price.Should().Be(49.99m);
        result.Currency.Should().Be("GBP");
        result.Sku.Should().Be("LAMP-001", "the SKU value object normalises to upper case");
    }

    [Fact]
    public async Task Handle_stamps_timestamps_from_the_injected_clock()
    {
        var result = await _sut.Handle(ValidCommand, CancellationToken.None);

        result.CreatedAt.Should().Be(FixedTimeProvider.DefaultNow);
        result.UpdatedAt.Should().Be(FixedTimeProvider.DefaultNow);
    }

    [Fact]
    public async Task Handle_adds_the_product_and_commits_exactly_once()
    {
        await _sut.Handle(ValidCommand, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_checks_uniqueness_against_the_normalised_sku()
    {
        // The uniqueness check must use the value object's normalised form,
        // otherwise "lamp-001" and "LAMP-001" would both be allowed.
        await _sut.Handle(ValidCommand, CancellationToken.None);

        await _repository.Received(1).SkuExistsAsync(
            Arg.Is<Sku>(s => s.Value == "LAMP-001"),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_throws_Conflict_when_the_sku_is_already_taken()
    {
        _repository
            .SkuExistsAsync(Arg.Any<Sku>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _sut.Handle(ValidCommand, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*LAMP-001*already exists*");
    }

    [Fact]
    public async Task Handle_does_not_persist_anything_when_the_sku_conflicts()
    {
        _repository
            .SkuExistsAsync(Arg.Any<Sku>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.Handle(ValidCommand, CancellationToken.None));

        await _repository.DidNotReceive().AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_honours_an_explicit_currency()
    {
        var command = ValidCommand with { Currency = "usd" };

        var result = await _sut.Handle(command, CancellationToken.None);

        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_raises_ProductCreated_on_the_aggregate_before_commit()
    {
        Product? captured = null;
        await _repository.AddAsync(
            Arg.Do<Product>(p => captured = p), Arg.Any<CancellationToken>());

        await _sut.Handle(ValidCommand, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_propagates_the_cancellation_token_to_the_repository()
    {
        // Cancellation must flow all the way down, otherwise an abandoned HTTP
        // request keeps doing database work nobody is waiting for.
        using var cts = new CancellationTokenSource();

        await _sut.Handle(ValidCommand, cts.Token);

        await _repository.Received(1).AddAsync(Arg.Any<Product>(), cts.Token);
        await _unitOfWork.Received(1).SaveChangesAsync(cts.Token);
    }
}
