using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Products.Application.Common.Abstractions;
using Products.Domain.Common;

namespace Products.Infrastructure.Persistence;

/// <summary>
/// Lets the EF Core CLI construct a <see cref="ProductsDbContext"/> at design
/// time, so migrations can be generated from the Infrastructure project without
/// booting the API.
/// </summary>
/// <remarks>
/// Without this, <c>dotnet ef</c> has to start the whole web host — which means
/// design-time tooling depends on the API's configuration, its secrets and its
/// start-up succeeding. Keeping migrations runnable from the project that owns
/// them is both faster and less brittle.
/// <para>
/// The connection string here is used only to pick the provider and build the
/// model. Migrations are generated from the model, not from the database, so
/// this never touches the real one.
/// </para>
/// </remarks>
internal sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlite(
                "Data Source=products-designtime.db",
                sqlite => sqlite.MigrationsAssembly(
                    typeof(ProductsDbContextFactory).Assembly.FullName))
            .Options;

        return new ProductsDbContext(options, NoOpDomainEventDispatcher.Instance);
    }

    /// <summary>
    /// Stands in for the real dispatcher at design time, where nothing is ever
    /// saved and so no event can be raised.
    /// </summary>
    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public static readonly NoOpDomainEventDispatcher Instance = new();

        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
