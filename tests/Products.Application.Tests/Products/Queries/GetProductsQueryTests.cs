using FluentValidation.TestHelper;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Application.Products.Dtos;
using Products.Application.Products.Queries.GetProducts;
using Products.Domain.Products;

namespace Products.Application.Tests.Products.Queries;

public class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new GetProductsQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_must_be_one_or_greater(int page)
    {
        _sut.TestValidate(new GetProductsQuery(Page: page))
            .ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageSize_must_be_one_or_greater(int pageSize)
    {
        _sut.TestValidate(new GetProductsQuery(PageSize: pageSize))
            .ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_is_capped_to_protect_the_server()
    {
        // Without the cap, ?pageSize=1000000 materialises the whole table.
        _sut.TestValidate(new GetProductsQuery(PageSize: PagingDefaults.MaxPageSize + 1))
            .ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_exactly_at_the_cap_is_accepted()
    {
        _sut.TestValidate(new GetProductsQuery(PageSize: PagingDefaults.MaxPageSize))
            .ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Sort_field_outside_the_allow_list_is_rejected()
    {
        // The allow-list is what stops a query-string value reaching an ORDER BY
        // clause as free text.
        _sut.TestValidate(new GetProductsQuery(SortBy: "'; DROP TABLE Products--"))
            .ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("Name")]
    [InlineData("PRICE")]
    [InlineData("createdAt")]
    [InlineData("colour")]
    [InlineData("color")]
    public void Allowed_sort_fields_are_accepted_case_insensitively(string sortBy)
    {
        _sut.TestValidate(new GetProductsQuery(SortBy: sortBy))
            .ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void Colour_outside_the_defined_set_is_rejected()
    {
        _sut.TestValidate(new GetProductsQuery(Colour: (ProductColour)999))
            .ShouldHaveValidationErrorFor(x => x.Colour);
    }

    [Fact]
    public void Absent_colour_means_no_filter_and_is_valid()
    {
        _sut.TestValidate(new GetProductsQuery(Colour: null))
            .ShouldNotHaveValidationErrorFor(x => x.Colour);
    }
}

public class GetProductsQueryHandlerTests
{
    private readonly IProductReadRepository _readRepository =
        Substitute.For<IProductReadRepository>();

    private readonly GetProductsQueryHandler _sut;

    public GetProductsQueryHandlerTests()
    {
        _sut = new GetProductsQueryHandler(_readRepository);

        _readRepository
            .GetPagedAsync(Arg.Any<ProductQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage);
    }

    private static PagedResult<ProductDto> EmptyPage => new()
    {
        Items = [],
        Page = 1,
        PageSize = 20,
        TotalCount = 0,
    };

    [Fact]
    public async Task Handle_passes_paging_and_sorting_through_to_the_read_side()
    {
        var query = new GetProductsQuery(Page: 3, PageSize: 50, SortBy: "price", SortDescending: true);

        await _sut.Handle(query, CancellationToken.None);

        await _readRepository.Received(1).GetPagedAsync(
            Arg.Is<ProductQueryOptions>(o =>
                o.Page == 3 &&
                o.PageSize == 50 &&
                o.SortBy == "price" &&
                o.SortDescending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_forwards_the_colour_filter_when_one_is_supplied()
    {
        await _sut.Handle(new GetProductsQuery(Colour: ProductColour.Red), CancellationToken.None);

        await _readRepository.Received(1).GetPagedAsync(
            Arg.Is<ProductQueryOptions>(o => o.Colour == ProductColour.Red),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_leaves_the_colour_filter_unset_when_none_is_supplied()
    {
        // "All products" and "red products" are the same code path with the
        // filter absent — that is the point of the shared handler.
        await _sut.Handle(new GetProductsQuery(), CancellationToken.None);

        await _readRepository.Received(1).GetPagedAsync(
            Arg.Is<ProductQueryOptions>(o => o.Colour == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_substitutes_the_default_sort_field_for_a_blank_one()
    {
        await _sut.Handle(new GetProductsQuery(SortBy: "  "), CancellationToken.None);

        await _readRepository.Received(1).GetPagedAsync(
            Arg.Is<ProductQueryOptions>(o => o.SortBy == ProductQueryOptions.DefaultSortField),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_an_empty_page_rather_than_null_when_nothing_matches()
    {
        // An empty result set is a valid answer, not an error. The API must
        // return 200 with [] so the frontend can show "no products" instead of
        // an error state.
        var result = await _sut.Handle(
            new GetProductsQuery(Colour: ProductColour.Gold), CancellationToken.None);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}

public class PagedResultTests
{
    private static PagedResult<int> Page(int page, int pageSize, int total) => new()
    {
        Items = [],
        Page = page,
        PageSize = pageSize,
        TotalCount = total,
    };

    [Theory]
    [InlineData(20, 100, 5)]
    [InlineData(20, 101, 6)]   // a partial final page still counts
    [InlineData(20, 0, 0)]
    [InlineData(7, 20, 3)]
    public void TotalPages_rounds_up_to_include_a_partial_final_page(
        int pageSize, int total, int expected)
    {
        Page(1, pageSize, total).TotalPages.Should().Be(expected);
    }

    [Fact]
    public void TotalPages_is_zero_when_the_page_size_is_invalid()
    {
        // Guards against a divide-by-zero if a page size of 0 ever slips past
        // validation.
        Page(1, 0, 10).TotalPages.Should().Be(0);
    }

    [Fact]
    public void Navigation_flags_reflect_position_within_the_result_set()
    {
        Page(1, 10, 35).HasPreviousPage.Should().BeFalse();
        Page(1, 10, 35).HasNextPage.Should().BeTrue();
        Page(2, 10, 35).HasPreviousPage.Should().BeTrue();
        Page(4, 10, 35).HasNextPage.Should().BeFalse();
    }
}
