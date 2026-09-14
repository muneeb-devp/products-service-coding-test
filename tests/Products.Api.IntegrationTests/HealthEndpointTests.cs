using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Covers the anonymous health endpoints required by the brief.
/// </summary>
public sealed class HealthEndpointTests(ProductsApiFactory factory)
    : IClassFixture<ProductsApiFactory>
{
    [Fact]
    public async Task Health_is_reachable_without_authentication()
    {
        // The requirement is explicit that this endpoint is anonymous — an
        // orchestrator probing it has no credentials to offer.
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_reports_the_database_connectivity_check()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("status").GetString().Should().Be("Healthy");

        var checks = payload.GetProperty("checks").EnumerateArray().ToArray();

        // Proves /health is a real probe rather than a hardcoded "OK": the
        // database check has to be present and passing.
        checks.Should().ContainSingle(c => c.GetProperty("name").GetString() == "database");
        checks.Should().OnlyContain(c => c.GetProperty("status").GetString() == "Healthy");
    }

    [Fact]
    public async Task Liveness_excludes_the_database_check()
    {
        // Liveness must not depend on the database. If it did, a database outage
        // would restart every healthy instance in a loop.
        var response = await factory.CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("checks").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Readiness_includes_the_database_check()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("checks").EnumerateArray()
            .Should().Contain(c => c.GetProperty("name").GetString() == "database");
    }

    [Fact]
    public async Task Health_does_not_leak_internal_detail()
    {
        // The endpoint is anonymous, so anything it returns is public. Connection
        // strings and stack traces must never appear here.
        var body = await factory.CreateClient().GetStringAsync("/health");

        body.Should().NotContainAny("DataSource", "Data Source", "Password", "Exception", "at ");
    }
}
