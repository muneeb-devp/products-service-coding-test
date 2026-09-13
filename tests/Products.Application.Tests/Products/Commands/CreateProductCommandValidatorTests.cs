using FluentValidation.TestHelper;
using Products.Application.Products.Commands.CreateProduct;
using Products.Domain.Products;

namespace Products.Application.Tests.Products.Commands;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _sut = new();

    private static CreateProductCommand Valid => new(
        "Ergonomic Desk Lamp", "A lamp.", ProductColour.Red, 49.99m, "LAMP-001");

    [Fact]
    public void A_well_formed_command_produces_no_failures()
    {
        _sut.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string? name)
    {
        _sut.TestValidate(Valid with { Name = name })
            .ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Product name is required.");
    }

    [Fact]
    public void Name_is_capped_at_the_domain_limit()
    {
        _sut.TestValidate(Valid with { Name = new string('x', Product.NameMaxLength + 1) })
            .ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Name_exactly_at_the_limit_is_accepted()
    {
        _sut.TestValidate(Valid with { Name = new string('x', Product.NameMaxLength) })
            .ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Description_is_capped_at_the_domain_limit()
    {
        _sut.TestValidate(
                Valid with { Description = new string('x', Product.DescriptionMaxLength + 1) })
            .ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_is_optional()
    {
        _sut.TestValidate(Valid with { Description = null })
            .ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Price_may_not_be_negative(decimal price)
    {
        _sut.TestValidate(Valid with { Price = price })
            .ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must be zero or greater.");
    }

    [Fact]
    public void Price_of_zero_is_accepted()
    {
        _sut.TestValidate(Valid with { Price = 0m })
            .ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_beyond_the_column_precision_is_rejected()
    {
        _sut.TestValidate(Valid with { Price = Money.MaxAmount + 1m })
            .ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_with_more_than_two_decimal_places_is_rejected()
    {
        // Rejected rather than silently rounded: a client sending 10.005 has a
        // bug, and quietly storing 10.01 hides it until someone reconciles.
        _sut.TestValidate(Valid with { Price = 10.005m })
            .ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must have no more than two decimal places.");
    }

    [Fact]
    public void Colour_outside_the_defined_set_is_rejected()
    {
        _sut.TestValidate(Valid with { Colour = (ProductColour)999 })
            .ShouldHaveValidationErrorFor(x => x.Colour);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sku_is_required(string? sku)
    {
        _sut.TestValidate(Valid with { Sku = sku })
            .ShouldHaveValidationErrorFor(x => x.Sku)
            .WithErrorMessage("SKU is required.");
    }

    [Theory]
    [InlineData("AB")]        // too short
    [InlineData("ABC 123")]   // space
    [InlineData("ABC_123")]   // underscore
    public void Malformed_skus_are_rejected(string sku)
    {
        _sut.TestValidate(Valid with { Sku = sku })
            .ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public void Currency_is_optional()
    {
        _sut.TestValidate(Valid with { Currency = null })
            .ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPP")]
    [InlineData("G3P")]
    public void Malformed_currency_codes_are_rejected(string currency)
    {
        _sut.TestValidate(Valid with { Currency = currency })
            .ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Every_failure_is_reported_in_one_pass()
    {
        // The client should see all the problems with their payload at once,
        // not discover them one round-trip at a time.
        var result = _sut.TestValidate(
            new CreateProductCommand(null, null, (ProductColour)999, -5m, null));

        result.Errors.Select(e => e.PropertyName)
            .Should().Contain(["Name", "Colour", "Price", "Sku"]);
    }
}
