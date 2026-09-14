using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Covers the product endpoints end to end: real HTTP, real middleware, real SQL.
/// </summary>
/// <remarks>
/// Implements <see cref="IAsyncLifetime"/> so every test starts against an empty
/// table. xUnit creates a new instance per test, so the reset runs per test and
/// the tests stay order-independent.
/// </remarks>
public sealed class ProductsEndpointTests(ProductsApiFactory factory)
    : IClassFixture<ProductsApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _client = await factory.CreateAuthenticatedClientAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Create_returns_201_with_a_Location_header_pointing_at_the_new_product()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", TestData.CreateProductPayload(sku: "NEW-001"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;

        // The Location header must actually resolve — a header that 404s is
        // worse than no header at all.
        var followed = await _client.GetAsync(response.Headers.Location);

        followed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await followed.Content.ReadFromJsonAsync<ProductResponse>())!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Create_persists_every_field_and_normalises_the_sku()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            name = "  Ergonomic Desk Lamp  ",
            description = "A lamp.",
            colour = "Red",
            price = 49.99m,
            sku = "lamp-001",
        });

        var created = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;

        created.Name.Should().Be("Ergonomic Desk Lamp", "the domain trims the name");
        created.Sku.Should().Be("LAMP-001", "the SKU value object upper-cases");
        created.Colour.Should().Be("Red");
        created.Price.Should().Be(49.99m);
        created.Currency.Should().Be("GBP", "GBP is the default currency");
        created.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_sku_with_409()
    {
        await TestData.CreateProductAsync(_client, sku: "DUP-001");

        var response = await _client.PostAsJsonAsync(
            "/api/products", TestData.CreateProductPayload(name: "Other", sku: "DUP-001"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_treats_differently_cased_skus_as_the_same_sku()
    {
        // Without normalisation these would be two rows, and the "unique SKU"
        // guarantee would be quietly false.
        await TestData.CreateProductAsync(_client, sku: "CASE-001");

        var response = await _client.PostAsJsonAsync(
            "/api/products", TestData.CreateProductPayload(name: "Other", sku: "case-001"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "Red", 10, "SKU-1", "Name")]          // empty name
    [InlineData("Valid", "Red", -1, "SKU-1", "Price")]    // negative price
    [InlineData("Valid", "Red", 10, "!!", "Sku")]         // malformed SKU
    [InlineData("Valid", "Red", 10, "", "Sku")]           // missing SKU
    public async Task Create_rejects_invalid_input_with_a_ProblemDetails_listing_the_field(
        string name, string colour, decimal price, string sku, string expectedField)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", new { name, colour, price, sku, description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        problem.GetProperty("status").GetInt32().Should().Be(400);
        problem.GetProperty("errors").TryGetProperty(expectedField, out _).Should().BeTrue(
            "the response should name the field that failed");
    }

    [Fact]
    public async Task Create_rejects_an_undefined_colour()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", TestData.CreateProductPayload(colour: "Tartan", sku: "COL-001"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_reports_all_validation_failures_at_once()
    {
        // A client should be able to fix everything in one pass rather than
        // discovering problems one request at a time.
        var response = await _client.PostAsJsonAsync(
            "/api/products", new { name = "", colour = "Red", price = -5, sku = "!!" });

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        problem.GetProperty("errors").EnumerateObject().Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Create_accepts_a_price_of_zero()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/products", TestData.CreateProductPayload(price: 0m, sku: "FREE-001"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_returns_all_products_as_json()
    {
        await TestData.CreateProductAsync(_client, name: "A", sku: "AAA-1");
        await TestData.CreateProductAsync(_client, name: "B", sku: "BBB-1");

        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>())!;

        page.TotalCount.Should().Be(2);
        page.Items.Select(i => i.Name).Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public async Task List_returns_an_empty_page_rather_than_404_when_there_are_no_products()
    {
        // "No products" is a valid answer with status 200, not an error. A 404
        // here would force the frontend to treat "empty catalogue" as a failure.
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>())!;

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task List_pages_through_results()
    {
        for (var i = 1; i <= 5; i++)
        {
            await TestData.CreateProductAsync(_client, name: $"Product {i}", sku: $"PAGE-{i}");
        }

        var page1 = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?page=1&pageSize=2"))!;
        var page3 = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?page=3&pageSize=2"))!;

        page1.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.TotalPages.Should().Be(3);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();

        page3.Items.Should().HaveCount(1, "the final page holds the remainder");
        page3.HasNextPage.Should().BeFalse();
        page3.HasPreviousPage.Should().BeTrue();

        page1.Items.Should().NotIntersectWith(page3.Items, "pages must not overlap");
    }

    [Fact]
    public async Task List_sorts_by_the_requested_field_and_direction()
    {
        await TestData.CreateProductAsync(_client, name: "Cheap", price: 5m, sku: "SORT-1");
        await TestData.CreateProductAsync(_client, name: "Expensive", price: 500m, sku: "SORT-2");
        await TestData.CreateProductAsync(_client, name: "Middle", price: 50m, sku: "SORT-3");

        var ascending = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?sortBy=price"))!;
        var descending = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?sortBy=price&sortDescending=true"))!;

        ascending.Items.Select(i => i.Price).Should().BeInAscendingOrder();
        descending.Items.Select(i => i.Price).Should().BeInDescendingOrder();
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("sortBy=; DROP TABLE Products--")]
    public async Task List_rejects_invalid_paging_and_sorting_parameters(string queryString)
    {
        // The sort field in particular: it must never reach the database as
        // free text.
        var response = await _client.GetAsync($"/api/products?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------- colour filter

    [Fact]
    public async Task Filtering_by_colour_returns_only_that_colour()
    {
        await TestData.CreateProductAsync(_client, name: "Red One", colour: "Red", sku: "RED-1");
        await TestData.CreateProductAsync(_client, name: "Red Two", colour: "Red", sku: "RED-2");
        await TestData.CreateProductAsync(_client, name: "Blue One", colour: "Blue", sku: "BLU-1");

        var page = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?colour=Red"))!;

        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(i => i.Colour == "Red");
    }

    [Fact]
    public async Task The_dedicated_colour_route_matches_the_query_string_filter()
    {
        await TestData.CreateProductAsync(_client, name: "Red One", colour: "Red", sku: "RED-1");
        await TestData.CreateProductAsync(_client, name: "Blue One", colour: "Blue", sku: "BLU-1");

        var viaRoute = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products/colour/Red"))!;
        var viaQuery = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?colour=Red"))!;

        // Both spellings reach the same query handler, so their results must be
        // identical — that is the point of not duplicating the logic.
        viaRoute.Items.Should().BeEquivalentTo(viaQuery.Items);
        viaRoute.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task The_american_spelling_is_accepted_as_an_alias()
    {
        await TestData.CreateProductAsync(_client, colour: "Green", sku: "GRN-1");

        var viaAlias = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products/color/Green"))!;

        viaAlias.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Filtering_by_a_colour_with_no_matches_returns_an_empty_page()
    {
        await TestData.CreateProductAsync(_client, colour: "Red", sku: "RED-1");

        var page = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?colour=Gold"))!;

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Filtering_by_an_undefined_colour_is_rejected()
    {
        var response = await _client.GetAsync("/api/products?colour=Tartan");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Colour_filtering_combines_with_paging()
    {
        for (var i = 1; i <= 3; i++)
        {
            await TestData.CreateProductAsync(_client, name: $"Red {i}", colour: "Red", sku: $"R-{i}");
        }

        await TestData.CreateProductAsync(_client, colour: "Blue", sku: "B-1");

        var page = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?colour=Red&page=1&pageSize=2"))!;

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3, "the count reflects the filter, not the whole table");
        page.Items.Should().OnlyContain(i => i.Colour == "Red");
    }

    // -------------------------------------------------------- get / update / delete

    [Fact]
    public async Task Get_by_id_returns_404_for_an_unknown_product()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Update_changes_the_product_and_advances_UpdatedAt()
    {
        var created = await TestData.CreateProductAsync(_client, sku: "UPD-001");

        var response = await _client.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            name = "Renamed",
            description = "New copy.",
            colour = "Blue",
            price = 99.99m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;

        updated.Name.Should().Be("Renamed");
        updated.Colour.Should().Be("Blue");
        updated.Price.Should().Be(99.99m);
        updated.UpdatedAt.Should().BeAfter(updated.CreatedAt);
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_product()
    {
        var response = await _client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", new
        {
            name = "Renamed",
            colour = "Blue",
            price = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_product()
    {
        var created = await TestData.CreateProductAsync(_client, sku: "DEL-001");

        var deleteResponse = await _client.DeleteAsync($"/api/products/{created.Id}");
        var getResponse = await _client.GetAsync($"/api/products/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_product()
    {
        var response = await _client.DeleteAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------- versioning

    [Fact]
    public async Task The_versioned_route_serves_the_same_resource_as_the_unversioned_one()
    {
        await TestData.CreateProductAsync(_client, sku: "VER-001");

        var unversioned = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products"))!;
        var versioned = (await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/v1/products"))!;

        versioned.Items.Should().BeEquivalentTo(unversioned.Items);
    }
}
