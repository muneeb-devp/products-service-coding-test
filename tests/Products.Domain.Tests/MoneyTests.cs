using Products.Domain.Exceptions;
using Products.Domain.Products;

namespace Products.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Create_defaults_to_GBP_when_no_currency_is_supplied()
    {
        Money.Create(10m).Currency.Should().Be(Money.DefaultCurrency);
    }

    [Fact]
    public void Create_upper_cases_the_currency_code()
    {
        Money.Create(10m, "usd").Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_allows_a_price_of_zero()
    {
        // Zero is a legitimate price — a free sample or promotional item.
        // Only negative prices are invalid.
        Money.Create(0m).Amount.Should().Be(0m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-9999)]
    public void Create_rejects_negative_amounts(decimal amount)
    {
        var act = () => Money.Create(amount);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*zero or greater*")
            .Which.PropertyName.Should().Be("Price");
    }

    [Fact]
    public void Create_rejects_amounts_beyond_the_supported_column_precision()
    {
        // Guards the decimal(18,2) column: without this the overflow surfaces
        // as a 500 from the database rather than a 400 from the API.
        var act = () => Money.Create(Money.MaxAmount + 1m);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*must not exceed*");
    }

    [Theory]
    [InlineData("GB")]      // too short
    [InlineData("GBPP")]    // too long
    [InlineData("G3P")]     // not all letters
    [InlineData("")]        // empty
    public void Create_rejects_malformed_currency_codes(string currency)
    {
        var act = () => Money.Create(10m, currency);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ISO 4217*");
    }

    [Theory]
    [InlineData(10.005, 10.01)]   // half rounds away from zero, not to even
    [InlineData(10.004, 10.00)]
    [InlineData(2.345, 2.35)]
    public void Create_rounds_to_two_decimal_places_away_from_zero(decimal input, decimal expected)
    {
        // Banker's rounding would give 10.00 for 10.005. Retail pricing expects
        // 10.01, and the value must match what the decimal(18,2) column stores
        // so that what we validated is what round-trips.
        Money.Create(input).Amount.Should().Be(expected);
    }

    [Fact]
    public void Equality_compares_both_amount_and_currency()
    {
        Money.Create(10m, "GBP").Should().Be(Money.Create(10m, "GBP"));
        Money.Create(10m, "GBP").Should().NotBe(Money.Create(10m, "USD"));
        Money.Create(10m, "GBP").Should().NotBe(Money.Create(11m, "GBP"));
    }

    [Fact]
    public void ToString_renders_amount_and_currency_invariantly()
    {
        Money.Create(1234.5m, "GBP").ToString().Should().Be("1234.50 GBP");
    }
}
