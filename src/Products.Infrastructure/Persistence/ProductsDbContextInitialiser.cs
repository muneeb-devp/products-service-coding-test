using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date at start-up and seeds demo data.
/// </summary>
public sealed class ProductsDbContextInitialiser(
    ProductsDbContext context,
    ILogger<ProductsDbContextInitialiser> logger)
{
    /// <summary>Applies any pending EF Core migrations.</summary>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Relational-only. The integration tests swap in providers that have
            // no migration story, and calling Migrate on those throws.
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.DatabaseInitialisationFailed(ex);
            throw;
        }
    }

    /// <summary>
    /// Seeds a small demo catalogue, but only when the table is empty.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await context.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Deliberately spread across colours so the colour filter has something
        // meaningful to demonstrate, and timestamps are staggered so the
        // default "newest first" ordering is visible.
        var seed = new[]
        {
            Product.Create("Ergonomic Desk Lamp", "Adjustable LED desk lamp with three colour temperatures.", ProductColour.Black, 49.99m, "LAMP-001", now.AddMinutes(-50)),
            Product.Create("Mechanical Keyboard", "Tenkeyless mechanical keyboard, tactile switches.", ProductColour.Black, 119.00m, "KEYB-001", now.AddMinutes(-45)),
            Product.Create("Wireless Mouse", "Six-button wireless mouse with USB-C charging.", ProductColour.Grey, 34.50m, "MOUS-001", now.AddMinutes(-40)),
            Product.Create("Laptop Stand", "Aluminium laptop stand, height adjustable.", ProductColour.Silver, 59.95m, "STND-001", now.AddMinutes(-35)),
            Product.Create("Cotton T-Shirt", "Heavyweight organic cotton t-shirt.", ProductColour.Red, 22.00m, "SHRT-RED-M", now.AddMinutes(-30)),
            Product.Create("Cotton T-Shirt", "Heavyweight organic cotton t-shirt.", ProductColour.Blue, 22.00m, "SHRT-BLU-M", now.AddMinutes(-25)),
            Product.Create("Running Shoes", "Lightweight road running shoes.", ProductColour.Red, 89.99m, "SHOE-001", now.AddMinutes(-20)),
            Product.Create("Water Bottle", "Insulated 750ml stainless steel bottle.", ProductColour.Green, 18.75m, "BOTL-001", now.AddMinutes(-15)),
            Product.Create("Notebook", "A5 dotted notebook, 180 pages.", ProductColour.Yellow, 9.99m, "NOTE-001", now.AddMinutes(-10)),
            Product.Create("Travel Backpack", "28-litre water-resistant backpack.", ProductColour.Blue, 74.00m, "BACK-001", now.AddMinutes(-5)),
        };

        // These rows are demo fixtures, not genuine business activity, so drop
        // the ProductCreated events the factory raised. Casting to DbContext to
        // "avoid the override" would not work: SaveChangesAsync is virtual, so
        // the override runs regardless of the static type.
        foreach (var product in seed)
        {
            product.ClearDomainEvents();
        }

        context.Products.AddRange(seed);

        await context.SaveChangesAsync(cancellationToken);

        logger.SeededProducts(seed.Length);
    }
}
