using Products.Domain.Common;
using Products.Domain.Exceptions;
using Products.Domain.Products.Events;

namespace Products.Domain.Products;

/// <summary>
/// A product in the catalogue. This is the aggregate root.
/// </summary>
public sealed class Product : Entity
{
    /// <summary>Longest permitted product name.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Longest permitted product description.</summary>
    public const int DescriptionMaxLength = 2_000;

    // Required by EF Core for materialisation. EF sets the properties directly
    // via their backing fields, so this bypasses the invariants by design:
    // rows already in the database were validated on the way in.
    private Product()
    {
        Name = string.Empty;
        Sku = null!;
    }

    private Product(
        Guid id,
        string name,
        string? description,
        ProductColour colour,
        Money price,
        Sku sku,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Colour = colour;
        Price = price;
        Sku = sku;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Display name. Required, trimmed, at most <see cref="NameMaxLength"/> characters.</summary>
    public string Name { get; private set; }

    /// <summary>Optional long-form description.</summary>
    public string? Description { get; private set; }

    /// <summary>The product's colour.</summary>
    public ProductColour Colour { get; private set; }

    /// <summary>Price and currency.</summary>
    public Money Price { get; private set; }

    /// <summary>The product's business identifier. Unique across the catalogue.</summary>
    public Sku Sku { get; private set; }

    /// <summary>When the product was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the product was last modified (UTC). Equal to <see cref="CreatedAt"/>
    /// until the first change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Creates a new product, enforcing every invariant and raising
    /// <see cref="ProductCreatedDomainEvent"/>.
    /// </summary>
    /// <param name="name">Display name. Required.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="colour">Colour; must be a defined <see cref="ProductColour"/>.</param>
    /// <param name="price">Price amount. Must be zero or greater.</param>
    /// <param name="sku">Business identifier.</param>
    /// <param name="currency">ISO 4217 currency code. Defaults to GBP.</param>
    /// <param name="now">
    /// The creation timestamp, supplied by the caller rather than read from
    /// <c>DateTimeOffset.UtcNow</c>. Injecting the clock keeps the domain
    /// deterministic and therefore genuinely testable.
    /// </param>
    /// <param name="id">
    /// Optional explicit identity, so a caller that needs to know the id before
    /// persisting (for an outbox record, say) can supply it.
    /// </param>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public static Product Create(
        string? name,
        string? description,
        ProductColour colour,
        decimal price,
        string? sku,
        DateTimeOffset now,
        string? currency = Money.DefaultCurrency,
        Guid? id = null)
    {
        var validatedName = ValidateName(name);
        var validatedDescription = ValidateDescription(description);
        ValidateColour(colour);

        // Money.Create and Sku.Create carry their own invariants, so the rules
        // live with the types they constrain rather than being restated here.
        var product = new Product(
            id ?? Guid.NewGuid(),
            validatedName,
            validatedDescription,
            colour,
            Money.Create(price, currency),
            Sku.Create(sku),
            now);

        product.RaiseDomainEvent(new ProductCreatedDomainEvent(
            product.Id, product.Sku.Value, product.Name, product.Colour, now));

        return product;
    }

    /// <summary>
    /// Updates the descriptive attributes of the product.
    /// </summary>
    /// <exception cref="DomainValidationException">Any invariant is violated.</exception>
    public void UpdateDetails(
        string? name,
        string? description,
        ProductColour colour,
        DateTimeOffset now)
    {
        var validatedName = ValidateName(name);
        var validatedDescription = ValidateDescription(description);
        ValidateColour(colour);

        // No-op guard: if nothing actually changed, do not bump UpdatedAt. An
        // idempotent PUT should not look like a modification to downstream
        // consumers watching that column.
        if (validatedName == Name && validatedDescription == Description && colour == Colour)
        {
            return;
        }

        Name = validatedName;
        Description = validatedDescription;
        Colour = colour;
        UpdatedAt = now;
    }

    /// <summary>
    /// Changes the product's price, raising
    /// <see cref="ProductPriceChangedDomainEvent"/> when the value actually moves.
    /// </summary>
    /// <exception cref="DomainValidationException">The price is invalid.</exception>
    public void ChangePrice(decimal newPrice, DateTimeOffset now, string? currency = null)
    {
        var next = Money.Create(newPrice, currency ?? Price.Currency);

        if (next == Price)
        {
            return;
        }

        var previous = Price;
        Price = next;
        UpdatedAt = now;

        RaiseDomainEvent(new ProductPriceChangedDomainEvent(
            Id, previous.Amount, next.Amount, next.Currency, now));
    }

    /// <summary>
    /// Marks the product as deleted by raising
    /// <see cref="ProductDeletedDomainEvent"/>. The caller performs the removal.
    /// </summary>
    public void MarkDeleted(DateTimeOffset now) =>
        RaiseDomainEvent(new ProductDeletedDomainEvent(Id, Sku.Value, now));

    private static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException(nameof(Name), "Product name is required.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainValidationException(
                nameof(Name),
                $"Product name must be {NameMaxLength} characters or fewer; got {trimmed.Length}.");
        }

        return trimmed;
    }

    private static string? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            // Normalise "absent" to null so empty string and null do not become
            // two different ways of saying the same thing.
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > DescriptionMaxLength)
        {
            throw new DomainValidationException(
                nameof(Description),
                $"Description must be {DescriptionMaxLength} characters or fewer; got {trimmed.Length}.");
        }

        return trimmed;
    }

    private static void ValidateColour(ProductColour colour)
    {
        // C# lets any int be cast to an enum, so (ProductColour)999 is a
        // perfectly legal value at the type level. Without this check it would
        // persist happily and blow up on read.
        if (!Enum.IsDefined(colour))
        {
            throw new DomainValidationException(
                nameof(Colour), $"'{(int)colour}' is not a valid product colour.");
        }
    }
}
