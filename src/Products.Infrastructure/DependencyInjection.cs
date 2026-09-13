using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Products.Application.Common.Abstractions;
using Products.Infrastructure.Persistence;
using Products.Infrastructure.Persistence.Repositories;

namespace Products.Infrastructure;

/// <summary>
/// Registers the Infrastructure layer with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Configuration key selecting the database provider.</summary>
    public const string ProviderConfigurationKey = "Database:Provider";

    /// <summary>Name of the connection string read from configuration.</summary>
    public const string ConnectionStringName = "ProductsDatabase";

    /// <summary>Health check name for database connectivity.</summary>
    public const string DatabaseHealthCheckName = "database";

    /// <summary>
    /// Adds the DbContext, repositories, domain event dispatcher and the
    /// database health check.
    /// </summary>
    /// <remarks>
    /// The provider is selected from configuration rather than compiled in, so
    /// moving from the SQLite file used for local development to SQL Server is
    /// a connection string and one setting — no code change and no rebuild.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration[ProviderConfigurationKey] ?? DatabaseProvider.Sqlite;
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fail loudly at start-up rather than on the first request. A
            // misconfigured connection string should stop a deployment, not
            // surface later as a confusing 500.
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddDbContext<ProductsDbContext>(options =>
        {
            if (string.Equals(provider, DatabaseProvider.SqlServer, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName);

                    // Transient faults are normal against a networked database
                    // (failover, throttling, a brief network blip). Retrying
                    // them is the difference between a hiccup and an outage.
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            }
            else if (string.Equals(provider, DatabaseProvider.Sqlite, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported database provider '{provider}'. " +
                    $"Expected '{DatabaseProvider.Sqlite}' or '{DatabaseProvider.SqlServer}'.");
            }
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<ProductsDbContextInitialiser>();

        // The unit of work is the same instance as the DbContext, resolved
        // through the interface so the Application layer never sees EF Core.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductsDbContext>());

        // A real connectivity probe, not a hardcoded "healthy". AddDbContextCheck
        // opens a connection and confirms the database actually answers, which
        // is what makes /health meaningful to an orchestrator.
        services.AddHealthChecks()
            .AddDbContextCheck<ProductsDbContext>(
                name: DatabaseHealthCheckName,
                tags: ["ready", "db"]);

        return services;
    }
}

/// <summary>Supported database provider names.</summary>
public static class DatabaseProvider
{
    /// <summary>SQLite — the default, used for local development and tests.</summary>
    public const string Sqlite = "Sqlite";

    /// <summary>SQL Server — the intended production provider.</summary>
    public const string SqlServer = "SqlServer";
}
