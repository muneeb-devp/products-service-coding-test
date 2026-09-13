using Products.Domain.Exceptions;
using Products.Domain.Products;
using Products.Domain.Products.Events;

namespace Products.Domain.Tests;

public class ProductTests
{
    // A fixed clock. The domain takes the timestamp as a parameter precisely so
    // these assertions can be exact instead of "within a few milliseconds".
    private static readonly DateTimeOffset Now =
        new(2026, 9, 14, 10, 30, 0, TimeSpan.Zero);

    private static Product CreateValidProduct(
        string name = "Ergonomic Desk Lamp",
        ProductColour colour = ProductColour.Red,
        decimal price = 49.99m,
        string sku = "LAMP-001") =>
        Product.Create(name, "A lamp.", colour, price, sku, Now);

    [Fact]
    public void Create_populates_every_field_and_stamps_both_timestamps()
    {
        var product = Product.Create(
            "Ergonomic Desk Lamp", "A lamp.", ProductColour.Red, 49.99m, "lamp-001", Now);

        product.Id.Should().NotBeEmpty();
        product.Name.Should().Be("Ergonomic Desk Lamp");
        product.Description.Should().Be("A lamp.");
        product.Colour.Should().Be(ProductColour.Red);
        product.Price.Amount.Should().Be(49.99m);
        product.Price.Currency.Should().Be("GBP");
        product.Sku.Value.Should().Be("LAMP-001", "the SKU value object normalises to upper case");
        product.CreatedAt.Should().Be(Now);
        product.UpdatedAt.Should().Be(Now, "a product is 'updated' at the moment it is created");
    }

    [Fact]
    public void Create_accepts_a_caller_supplied_identity()
    {
        var id = Guid.NewGuid();

        Product.Create("Lamp", null, ProductColour.Red, 1m, "L-1", Now, id: id)
            .Id.Should().Be(id);
    }

    [Fact]
    public void Create_raises_ProductCreated_carrying_the_new_state()
    {
        var product = CreateValidProduct();

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductCreatedDomainEvent>()
            .Which.Should().Match<ProductCreatedDomainEvent>(e =>
                e.ProductId == product.Id &&
                e.Sku == "LAMP-001" &&
                e.Name == "Ergonomic Desk Lamp" &&
                e.Colour == ProductColour.Red &&
                e.OccurredOn == Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_missing_name(string? name)
    {
        var act = () => Product.Create(name, null, ProductColour.Red, 1m, "SKU-1", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*name is required*")
            .Which.PropertyName.Should().Be("Name");
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_the_limit()
    {
        var tooLong = new string('x', Product.NameMaxLength + 1);

        var act = () => Product.Create(tooLong, null, ProductColour.Red, 1m, "SKU-1", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*{Product.NameMaxLength} characters or fewer*");
    }

    [Fact]
    public void Create_accepts_a_name_exactly_at_the_limit()
    {
        // Boundary: NameMaxLength itself must be allowed, only >max rejected.
        var atLimit = new string('x', Product.NameMaxLength);

        Product.Create(atLimit, null, ProductColour.Red, 1m, "SKU-1", Now)
            .Name.Should().HaveLength(Product.NameMaxLength);
    }

    [Fact]
    public void Create_trims_surrounding_whitespace_from_the_name()
    {
        Product.Create("  Lamp  ", null, ProductColour.Red, 1m, "SKU-1", Now)
            .Name.Should().Be("Lamp");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_normalises_a_blank_description_to_null(string? description)
    {
        // "absent" must have exactly one representation, otherwise null and ""
        // become two different ways of saying the same thing.
        Product.Create("Lamp", description, ProductColour.Red, 1m, "SKU-1", Now)
            .Description.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_a_description_longer_than_the_limit()
    {
        var tooLong = new string('x', Product.DescriptionMaxLength + 1);

        var act = () => Product.Create("Lamp", tooLong, ProductColour.Red, 1m, "SKU-1", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage($"*{Product.DescriptionMaxLength} characters or fewer*");
    }

    [Fact]
    public void Create_rejects_a_negative_price()
    {
        var act = () => Product.Create("Lamp", null, ProductColour.Red, -0.01m, "SKU-1", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*zero or greater*");
    }

    [Fact]
    public void Create_rejects_a_colour_outside_the_defined_set()
    {
        // C# permits any int to be cast to an enum, so this is a legal value at
        // the type level and would otherwise persist and fail on read.
        var act = () => Product.Create("Lamp", null, (ProductColour)999, 1m, "SKU-1", Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*not a valid product colour*");
    }

    [Fact]
    public void Create_rejects_an_invalid_sku()
    {
        var act = () => Product.Create("Lamp", null, ProductColour.Red, 1m, "!!", Now);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void UpdateDetails_applies_changes_and_advances_UpdatedAt()
    {
        var product = CreateValidProduct();
        var later = Now.AddHours(2);

        product.UpdateDetails("Renamed Lamp", "New copy.", ProductColour.Blue, later);

        product.Name.Should().Be("Renamed Lamp");
        product.Description.Should().Be("New copy.");
        product.Colour.Should().Be(ProductColour.Blue);
        product.UpdatedAt.Should().Be(later);
        product.CreatedAt.Should().Be(Now, "creation time is immutable");
    }

    [Fact]
    public void UpdateDetails_is_a_no_op_when_nothing_actually_changed()
    {
        var product = CreateValidProduct();

        product.UpdateDetails("Ergonomic Desk Lamp", "A lamp.", ProductColour.Red, Now.AddHours(2));

        // An idempotent PUT must not look like a modification to anything
        // downstream that watches UpdatedAt.
        product.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void UpdateDetails_enforces_the_same_invariants_as_creation()
    {
        var product = CreateValidProduct();

        var act = () => product.UpdateDetails("", null, ProductColour.Red, Now);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*name is required*");
    }

    [Fact]
    public void ChangePrice_updates_the_price_and_raises_an_event_with_both_values()
    {
        var product = CreateValidProduct(price: 49.99m);
        product.ClearDomainEvents();
        var later = Now.AddDays(1);

        product.ChangePrice(59.99m, later);

        product.Price.Amount.Should().Be(59.99m);
        product.UpdatedAt.Should().Be(later);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductPriceChangedDomainEvent>()
            .Which.Should().Match<ProductPriceChangedDomainEvent>(e =>
                e.OldPrice == 49.99m && e.NewPrice == 59.99m && e.Currency == "GBP");
    }

    [Fact]
    public void ChangePrice_to_the_same_value_raises_nothing_and_leaves_UpdatedAt_alone()
    {
        var product = CreateValidProduct(price: 49.99m);
        product.ClearDomainEvents();

        product.ChangePrice(49.99m, Now.AddDays(1));

        product.DomainEvents.Should().BeEmpty("nothing changed, so nothing happened");
        product.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void ChangePrice_keeps_the_existing_currency_when_none_is_given()
    {
        var product = Product.Create("Lamp", null, ProductColour.Red, 10m, "SKU-1", Now, currency: "USD");

        product.ChangePrice(20m, Now.AddDays(1));

        product.Price.Currency.Should().Be("USD");
    }

    [Fact]
    public void ChangePrice_rejects_a_negative_price()
    {
        var product = CreateValidProduct();

        var act = () => product.ChangePrice(-1m, Now);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void MarkDeleted_raises_ProductDeleted()
    {
        var product = CreateValidProduct();
        product.ClearDomainEvents();

        product.MarkDeleted(Now);

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductDeletedDomainEvent>();
    }

    [Fact]
    public void ClearDomainEvents_empties_the_pending_log()
    {
        var product = CreateValidProduct();
        product.DomainEvents.Should().NotBeEmpty();

        product.ClearDomainEvents();

        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Entities_with_the_same_id_are_equal_regardless_of_other_state()
    {
        var id = Guid.NewGuid();
        var a = Product.Create("A", null, ProductColour.Red, 1m, "SKU-A", Now, id: id);
        var b = Product.Create("B", null, ProductColour.Blue, 2m, "SKU-B", Now, id: id);

        a.Should().Be(b, "entity equality is identity equality");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        CreateValidProduct().Should().NotBe(CreateValidProduct());
    }
}
