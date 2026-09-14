using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Covers the security boundary: that secured endpoints really are secured, and
/// that a valid token really does open them.
/// </summary>
public sealed class AuthenticationTests(ProductsApiFactory factory)
    : IClassFixture<ProductsApiFactory>
{
    public static TheoryData<string, string> SecuredEndpoints => new()
    {
        { "GET", "/api/products" },
        { "GET", "/api/products/colour/Red" },
        { "GET", "/api/products/00000000-0000-0000-0000-000000000001" },
        { "POST", "/api/products" },
        { "PUT", "/api/products/00000000-0000-0000-0000-000000000001" },
        { "DELETE", "/api/products/00000000-0000-0000-0000-000000000001" },
    };

    [Theory]
    [MemberData(nameof(SecuredEndpoints))]
    public async Task Every_product_endpoint_rejects_an_unauthenticated_request(
        string method, string path)
    {
        // Enumerated rather than spot-checked: it is easy to add an endpoint and
        // forget [Authorize], and the failure mode is an open door.
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_endpoint_issues_a_bearer_token_for_valid_credentials()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { username = ProductsApiFactory.TestUsername, password = ProductsApiFactory.TestPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("tokenType").GetString().Should().Be("Bearer");
        payload.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Token_endpoint_rejects_an_incorrect_password()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { username = ProductsApiFactory.TestUsername, password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_endpoint_rejects_an_unknown_user()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { username = "no-such-user", password = ProductsApiFactory.TestPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_endpoint_does_not_reveal_whether_the_username_exists()
    {
        // Different messages for "unknown user" and "wrong password" would let an
        // attacker enumerate valid usernames.
        var unknownUser = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token", new { username = "no-such-user", password = "whatever" });

        var wrongPassword = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token",
            new { username = ProductsApiFactory.TestUsername, password = "whatever" });

        var unknownBody = await unknownUser.Content.ReadAsStringAsync();
        var wrongBody = await wrongPassword.Content.ReadAsStringAsync();

        unknownUser.StatusCode.Should().Be(wrongPassword.StatusCode);

        // Compare the stable parts; traceId differs per request by design.
        StripTraceId(unknownBody).Should().Be(StripTraceId(wrongBody));
    }

    [Fact]
    public async Task Token_endpoint_requires_both_username_and_password()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/token", new { username = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_valid_token_opens_the_secured_endpoints()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_structurally_invalid_token_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_signed_with_the_wrong_key_is_rejected()
    {
        // The signature check is the whole point of JWT validation. A token with
        // correct claims but a foreign signature must not be accepted.
        var forged = CreateForgedToken();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_me_endpoint_echoes_the_authenticated_subject()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        payload.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        payload.GetProperty("subject").GetString().Should().Be(ProductsApiFactory.TestUsername);
    }

    private static string CreateForgedToken()
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("a-completely-different-signing-key-32-bytes"));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "products-api-tests",
            audience: "products-api-tests",
            claims: [new System.Security.Claims.Claim("sub", ProductsApiFactory.TestUsername)],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    private static string StripTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");
}
