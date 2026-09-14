using FluentValidation;
using MediatR;
using ValidationException = Products.Application.Common.Exceptions.ValidationException;

namespace Products.Application.Common.Behaviours;

/// <summary>
/// Runs every FluentValidation validator registered for a request before the
/// handler executes.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IValidator<TRequest>[] _validators = validators.ToArray();

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            // Grouped by property so the payload matches the shape
            // ValidationProblemDetails expects: { "Name": ["msg1", "msg2"] }.
            var errors = failures
                .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);

            throw new ValidationException(errors);
        }

        return await next(cancellationToken);
    }
}
