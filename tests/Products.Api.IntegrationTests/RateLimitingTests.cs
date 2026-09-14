using System.Net;
using System.Net.Http.Json;

namespace Products.Api.IntegrationTests;

/// <summary>
/// A host with deliberately tiny rate limits, so the throttle can be tripped on
/// purpose rather than by accident.
/// </summary>
internal sealed class ThrottledApiFactory : ProductsApiFactory
{
    protected override (int WritePermits, int AuthPermits) RateLimits => (3, 3);
}

/// <summary>
/// Verifies that the rate limiter actually rejects excess traffic, and does so
/// with a response a client can act on.
/// </summary>
public sealed class RateLimitingTests : IAsyncLifetime, IDisposable
{
    private readonly ThrottledApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.ResetDatabaseAsync();
        _client = await _factory.CreateAuthenticatedClientAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Satisfies CA1001: this class owns the factory it creates.
    /// </summary>
    /// <remarks>
    /// The real teardown is in <see cref="DisposeAsync"/>, which xUnit awaits.
    /// This exists so the ownership is declared through the type system too.
    /// </remarks>
    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Writes_beyond_the_limit_are_rejected_with_429()
    {
        var statuses = new List<HttpStatusCode>();

        // Limit is 3 per window; the fourth must be rejected.
        for (var i = 1; i <= 5; i++)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products", TestData.CreateProductPayload(sku: $"RL-{i}"));

            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
        statuses.Count(s => s == HttpStatusCode.Created).Should().Be(3);
    }

    [Fact]
    public async Task A_throttled_response_tells_the_client_when_to_retry()
    {
        HttpResponseMessage? throttled = null;

        for (var i = 1; i <= 6 && throttled is null; i++)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/products", TestData.CreateProductPayload(sku: $"RLH-{i}"));

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled = response;
            }
        }

        throttled.Should().NotBeNull("the limit should have been reached");

        // Without Retry-After the client can only guess, and guessing usually
        // means retrying immediately and staying throttled.
        throttled!.Headers.RetryAfter.Should().NotBeNull();
        throttled.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Reads_are_not_throttled_by_the_write_policy()
    {
        // Reads are cheap and idempotent. Throttling them alongside writes would
        // degrade the frontend for no security or capacity benefit.
        for (var i = 1; i <= 10; i++)
        {
            var response = await _client.GetAsync("/api/products");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
