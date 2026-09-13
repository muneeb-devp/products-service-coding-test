using FluentValidation;
using Products.Application.Common.Abstractions;
using Products.Application.Common.Models;
using Products.Domain.Products;

namespace Products.Application.Products.Queries.GetProducts;

/// <summary>
/// Validates paging, sorting and filter parameters for <see cref="GetProductsQuery"/>.
/// </summary>
public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("Page size must be 1 or greater.")
            .LessThanOrEqualTo(PagingDefaults.MaxPageSize)
                .WithMessage($"Page size must not exceed {PagingDefaults.MaxPageSize}.");

        RuleFor(x => x.SortBy)
            .Must(field => ProductQueryOptions.AllowedSortFields.Contains(field!))
            .WithMessage(_ =>
                "Sort field must be one of: " +
                $"{string.Join(", ", ProductQueryOptions.AllowedSortFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));

        RuleFor(x => x.Colour)
            .Must(c => Enum.IsDefined(c!.Value))
            .WithMessage(_ =>
                $"Colour must be one of: {string.Join(", ", Enum.GetNames<ProductColour>())}.")
            .When(x => x.Colour.HasValue);
    }
}
