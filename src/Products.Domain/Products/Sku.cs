using System.Text.RegularExpressions;
using Products.Domain.Exceptions;

namespace Products.Domain.Products;

/// <summary>
/// A Stock Keeping Unit: the human-readable business identifier for a product.
/// </summary>
/// <remarks>
/// A value object rather than a bare <see cref="string"/>. Two benefits that a
/// string cannot give us:
/// <list type="number">
///   <item>It is impossible to construct an invalid SKU — validation lives in
///         the only constructor, so any <see cref="Sku"/> in the system is valid
///         by definition.</item>
///   <item>It normalises on the way in (trim + upper-case), so <c>"abc-123"</c>
///         and <c>" ABC-123 "</c> cannot both exist as distinct rows behind a
///         unique index.</item>
/// </list>
/// </remarks>
public sealed partial record Sku
{
    /// <summary>Shortest permitted SKU length.</summary>
    public const int MinLength = 3;

    /// <summary>Longest permitted SKU length.</summary>
    public const int MaxLength = 32;

    private Sku(string value) => Value = value;

    /// <summary>The normalised SKU text (trimmed, upper-cased).</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a SKU, normalising and validating the input.
    /// </summary>
    /// <exception cref="DomainValidationException">The value is empty, the wrong
    /// length, or contains characters outside <c>A-Z</c>, <c>0-9</c> and <c>-</c>.</exception>
    public static Sku Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(nameof(Sku), "SKU is required.");
        }

        var normalised = value.Trim().ToUpperInvariant();

        if (normalised.Length is < MinLength or > MaxLength)
        {
            throw new DomainValidationException(
                nameof(Sku),
                $"SKU must be between {MinLength} and {MaxLength} characters; got {normalised.Length}.");
        }

        if (!SkuPattern().IsMatch(normalised))
        {
            throw new DomainValidationException(
                nameof(Sku),
                "SKU may only contain letters, digits and hyphens.");
        }

        return new Sku(normalised);
    }

    /// <summary>
    /// Non-throwing counterpart to <see cref="Create"/>, for validation paths
    /// where an exception would be control flow rather than an error.
    /// </summary>
    public static bool TryCreate(string? value, out Sku? sku)
    {
        try
        {
            sku = Create(value);
            return true;
        }
        catch (DomainValidationException)
        {
            sku = null;
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Unwraps the SKU to its underlying text.</summary>
    public static implicit operator string(Sku sku) => sku.Value;

    // Source-generated for speed: compiled once at build time rather than
    // re-parsed on every call.
    [GeneratedRegex("^[A-Z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();
}
