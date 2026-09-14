using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Exceptions;
using Products.Domain.Common;
using Products.Domain.Products;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// The EF Core context for the Products bounded context, and the unit of work
/// for a single business transaction.
/// </summary>
public sealed class ProductsDbContext(
    DbContextOptions<ProductsDbContext> options,
    IDomainEventDispatcher domainEventDispatcher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>The product catalogue.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in this assembly, so adding a
        // new aggregate means adding a configuration file, not editing this one.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Commits the transaction, then dispatches any domain events raised during it.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToArray();

        var domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToArray();

        int result;

        try
        {
            result = await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // The unique index on Sku is the real guard against duplicates; the
            // application layer's pre-check can be lost to a race between two
            // concurrent requests. Translating here means that race still
            // surfaces as a clean 409 rather than an opaque 500.
            throw new ConflictException(
                "A product with that SKU already exists.");
        }

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }

    // IUnitOfWork.SaveChangesAsync is satisfied by the override above; the
    // interface exists so the Application layer can commit without referencing
    // EF Core at all.

    /// <summary>
    /// Whether a failed save was caused by a unique-constraint violation.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException switch
        {
            Microsoft.Data.SqlClient.SqlException sql => sql.Number is 2601 or 2627,
            Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteErrorCode is 19
                || sqlite.SqliteExtendedErrorCode is 2067,
            _ => false,
        };
}
