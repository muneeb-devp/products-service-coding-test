using FluentValidation;
using Products.Domain.Products;

namespace Products.Application.Products.Commands.UpdateProduct;

/// <summary>Validates <see cref="UpdateProductCommand"/>.</summary>
public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Product id is required.");

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
            .Must(p => decimal.Round(p, 2) == p)
                .WithMessage("Price must have no more than two decimal places.");

        RuleFor(x => x.Currency)
            .Length(Money.CurrencyCodeLength)
                .WithMessage($"Currency must be a {Money.CurrencyCodeLength}-letter ISO 4217 code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
    }
}
