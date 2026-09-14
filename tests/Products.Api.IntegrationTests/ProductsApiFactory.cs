using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Products.Infrastructure.Persistence;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Boots the real API in memory for integration testing.
/// </summary>
public class ProductsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Username the test host accepts at the demo token endpoint.</summary>
    public const string TestUsername = "test-user";

    /// <summary>Password the test host accepts at the demo token endpoint.</summary>
    public const string TestPassword = "test-password-123!";

    // Held open for the lifetime of the factory. A SQLite in-memory database
    // exists only while a connection to it is open, let this close and the
    // schema vanishes mid-test-run.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // One token reused across the class. Requesting a fresh one per test would
    // run the suite straight into the token endpoint's own rate limit, which is
    // a correct limit, not a bug to work around.
    private string? _cachedToken;

    /// <summary>
    /// Rate limits applied to this host.
    /// </summary>
    protected virtual (int WritePermits, int AuthPermits) RateLimits => (10_000, 10_000);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A signing key distinct from the development one, so a test
                // can never accidentally pass by validating a token minted with
                // the committed dev key.
                ["Jwt:Issuer"] = "products-api-tests",
                ["Jwt:Audience"] = "products-api-tests",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-bytes-long",
                ["Jwt:TokenLifetimeMinutes"] = "10",

                ["DemoCredentials:Username"] = TestUsername,
                ["DemoCredentials:Password"] = TestPassword,

                // Tests create the data they assert on, so the demo catalogue
                // would only be noise in every count.
                ["Database:SeedDemoData"] = "false",
                ["ConnectionStrings:ProductsDatabase"] = "DataSource=:memory:",

                ["RateLimiting:Writes:PermitLimit"] =
                    RateLimits.WritePermits.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Writes:WindowSeconds"] = "60",
                ["RateLimiting:Authentication:PermitLimit"] =
                    RateLimits.AuthPermits.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Authentication:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Drop the app's registration and re-point the context at the shared
            // connection this factory owns.
            services.RemoveAll<DbContextOptions<ProductsDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ProductsDbContext>();

            services.AddDbContext<ProductsDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Opens the shared connection before the host starts.</summary>
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        // Touching Services builds the host, which runs the start-up migration
        // against the now-open connection.
        _ = Services;
    }

    /// <summary>Creates a client carrying a valid bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        _cachedToken ??= await RequestTokenAsync(client);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _cachedToken);

        return client;
    }

    /// <summary>Obtains a token from the real token endpoint.</summary>
    public static async Task<string> RequestTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/token",
            new { username = TestUsername, password = TestPassword });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenPayload>();

        return payload?.AccessToken
            ?? throw new InvalidOperationException("Token endpoint returned no access token.");
    }

    /// <summary>
    /// Empties the Products table so each test starts from a known state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();

        await context.Products.ExecuteDeleteAsync();
    }

    /// <inheritdoc />
    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed record TokenPayload(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);
}
