using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Product"/> aggregate to its table.
/// </summary>
/// <remarks>
/// Mapping lives here rather than as attributes on the entity, which is what
/// keeps the Domain project free of any EF Core reference. The domain describes
/// the business; this file describes how it is stored.
/// </remarks>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        // Client-generated: the aggregate assigns its own id in Product.Create,
        // so the id is known before the insert. That matters for the outbox
        // pattern, where the event must reference the product it describes.
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(Product.NameMaxLength);

        builder.Property(p => p.Description)
            .HasMaxLength(Product.DescriptionMaxLength);

        // Stored by NAME, not ordinal. Persisting the ordinal would mean that
        // inserting a new colour into the middle of the enum silently re-labels
        // every existing row.
        builder.Property(p => p.Colour)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Money is a value object, but it lands in two ordinary columns rather
        // than a join. The aggregate gets a type that enforces its own rules;
        // the database gets a plain decimal and a currency code.
        builder.ComplexProperty(p => p.Price, price =>
        {
            price.Property(m => m.Amount)
                .HasColumnName("Price")
                .HasPrecision(18, 2)
                .IsRequired();

            price.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(Money.CurrencyCodeLength)
                .IsRequired();
        });

        // The SKU value object round-trips through its own factory on the way
        // back, so a row loaded from the database is validated and normalised by
        // exactly the same code that validated it on the way in.
        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(Sku.MaxLength)
            .HasConversion(
                sku => sku.Value,
                value => Sku.Create(value));

        // The real enforcement of SKU uniqueness. The application layer's
        // pre-check gives the common case a clean 409, but two concurrent
        // requests can both pass it — only this index actually prevents the
        // duplicate.
        builder.HasIndex(p => p.Sku)
            .IsUnique()
            .HasDatabaseName("IX_Products_Sku");

        // Supports the colour filter. Without it, filtering by colour is a full
        // table scan on every request.
        builder.HasIndex(p => p.Colour)
            .HasDatabaseName("IX_Products_Colour");

        // Supports the default sort (newest first), so paging does not have to
        // sort the whole table to return twenty rows.
        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_Products_CreatedAt");

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // Domain events are in-memory bookkeeping for the current transaction,
        // never a persisted column.
        builder.Ignore(p => p.DomainEvents);
    }
}
