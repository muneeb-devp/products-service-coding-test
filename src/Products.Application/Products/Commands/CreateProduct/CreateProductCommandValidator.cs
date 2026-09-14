using FluentValidation;
using Products.Domain.Products;

namespace Products.Application.Products.Commands.CreateProduct;

/// <summary>
/// Validates <see cref="CreateProductCommand"/> at the application boundary.
/// </summary>
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(Product.NameMaxLength)
                .WithMessage($"Product name must be {Product.NameMaxLength} characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(Product.DescriptionMaxLength)
                .WithMessage($"Description must be {Product.DescriptionMaxLength} characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Colour)
            .Must(c => Enum.IsDefined(c))
            .WithMessage(_ =>
                $"Colour must be one of: {string.Join(", ", Enum.GetNames<ProductColour>())}.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0m).WithMessage("Price must be zero or greater.")
            .LessThanOrEqualTo(Money.MaxAmount)
                .WithMessage($"Price must not exceed {Money.MaxAmount}.")
            .Must(HaveNoMoreThanTwoDecimalPlaces)
                .WithMessage("Price must have no more than two decimal places.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.");

        // Deliberately a separate chain. FluentValidation applies .When() to
        // every rule that precedes it in the same chain, so folding this into
        // the NotEmpty() chain above would switch the "required" rule off in
        // exactly the case it exists to catch: an absent SKU.
        RuleFor(x => x.Sku)
            .Must(sku => Sku.TryCreate(sku, out _))
                .WithMessage(
                    $"SKU must be {Sku.MinLength}-{Sku.MaxLength} characters and contain only " +
                    "letters, digits and hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.Sku));

        RuleFor(x => x.Currency)
            .Length(Money.CurrencyCodeLength)
                .WithMessage($"Currency must be a {Money.CurrencyCodeLength}-letter ISO 4217 code.")
            .Must(c => c!.All(char.IsAsciiLetter))
                .WithMessage("Currency must contain only letters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
    }

    // Rejected at the boundary rather than silently rounded. A client sending
    // 10.005 has a bug, and quietly storing 10.01 hides it until someone
    // reconciles the totals.
    private static bool HaveNoMoreThanTwoDecimalPlaces(decimal price) =>
        decimal.Round(price, 2) == price;
}
