using System.Globalization;
using Products.Domain.Exceptions;

namespace Products.Domain.Products;

/// <summary>
/// A monetary amount together with the currency it is denominated in.
/// </summary>
/// <remarks>
/// A bare <c>decimal Price</c> is the classic primitive-obsession bug: it lets
/// you add GBP to USD and get a meaningless number, and it puts the "price must
/// not be negative" rule in whatever layer happens to remember it. Wrapping the
/// pair makes the invalid states unrepresentable and gives the rule one home.
/// <para>
/// Stored as an EF Core owned type, so it still lands in two ordinary columns
/// (<c>Price</c>, <c>Currency</c>) rather than a join.
/// </para>
/// </remarks>
public readonly record struct Money
{
    /// <summary>Currency codes are ISO 4217 — always three letters.</summary>
    public const int CurrencyCodeLength = 3;

    /// <summary>Currency used when a caller does not specify one.</summary>
    public const string DefaultCurrency = "GBP";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount, never negative, rounded to two decimal places.</summary>
    public decimal Amount { get; }

    /// <summary>The ISO 4217 currency code, upper-case (e.g. <c>GBP</c>).</summary>
    public string Currency { get; }

    /// <summary>
    /// Creates a monetary value.
    /// </summary>
    /// <param name="amount">The amount. Must be zero or greater.</param>
    /// <param name="currency">ISO 4217 code; defaults to <see cref="DefaultCurrency"/>.</param>
    /// <exception cref="DomainValidationException">
    /// The amount is negative, or the currency code is not three letters.
    /// </exception>
    public static Money Create(decimal amount, string? currency = DefaultCurrency)
    {
        // A price of zero is legitimate (a free sample, a promotional item);
        // a negative price is not.
        if (amount < 0m)
        {
            throw new DomainValidationException(
                PriceProperty, $"Price must be zero or greater; got {amount}.");
        }

        // Guard the top end too. Without this, a fat-fingered price silently
        // overflows the decimal(18,2) column at the database and fails as a
        // 500 rather than a 400.
        if (amount > MaxAmount)
        {
            throw new DomainValidationException(
                PriceProperty, $"Price must not exceed {MaxAmount}.");
        }

        var code = (currency ?? DefaultCurrency).Trim().ToUpperInvariant();

        if (code.Length != CurrencyCodeLength || !code.All(char.IsAsciiLetter))
        {
            throw new DomainValidationException(
                nameof(Currency),
                $"Currency must be a {CurrencyCodeLength}-letter ISO 4217 code; got '{currency}'.");
        }

        // Round half-away-from-zero: the convention retail pricing expects, and
        // it matches the decimal(18,2) column so the value that round-trips from
        // the database is the value we validated.
        return new Money(Math.Round(amount, 2, MidpointRounding.AwayFromZero), code);
    }

    /// <summary>Upper bound for a single product price.</summary>
    public const decimal MaxAmount = 9_999_999.99m;

    // The domain type is "Money" but the field a client actually sends is
    // "Price". Report the latter, so the error points at the JSON they wrote.
    private const string PriceProperty = "Price";

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");
}
