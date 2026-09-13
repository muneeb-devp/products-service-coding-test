using Products.Domain.Exceptions;
using Products.Domain.Products;

namespace Products.Domain.Tests;

public class SkuTests
{
    [Theory]
    [InlineData("abc-123", "ABC-123")]
    [InlineData("  WID-001  ", "WID-001")]
    [InlineData("Widget001", "WIDGET001")]
    public void Create_normalises_case_and_whitespace(string input, string expected)
    {
        // Normalisation is the whole point of the value object: without it,
        // "abc-123" and "ABC-123" become two rows behind a unique index.
        Sku.Create(input).Value.Should().Be(expected);
    }

    [Fact]
    public void Create_produces_value_equality_not_reference_equality()
    {
        var a = Sku.Create("SKU-1");
        var b = Sku.Create("sku-1");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        ReferenceEquals(a, b).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_values(string? input)
    {
        var act = () => Sku.Create(input);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*required*");
    }

    [Theory]
    [InlineData("AB")]                                    // shorter than MinLength
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")]  // longer than MaxLength
    public void Create_rejects_out_of_range_lengths(string input)
    {
        var act = () => Sku.Create(input);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*between*characters*");
    }

    [Theory]
    [InlineData("ABC 123")]   // space
    [InlineData("ABC_123")]   // underscore
    [InlineData("ABC/123")]   // slash
    [InlineData("ABC#123")]   // hash
    public void Create_rejects_disallowed_characters(string input)
    {
        var act = () => Sku.Create(input);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*letters, digits and hyphens*");
    }

    [Fact]
    public void TryCreate_reports_failure_without_throwing()
    {
        Sku.TryCreate("!!", out var sku).Should().BeFalse();
        sku.Should().BeNull();
    }

    [Fact]
    public void TryCreate_yields_the_normalised_value_on_success()
    {
        Sku.TryCreate("ok-1", out var sku).Should().BeTrue();
        sku!.Value.Should().Be("OK-1");
    }

    [Fact]
    public void Implicit_conversion_unwraps_to_the_underlying_string()
    {
        string value = Sku.Create("ABC-123");

        value.Should().Be("ABC-123");
    }
}
