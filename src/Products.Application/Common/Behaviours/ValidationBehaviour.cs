using FluentValidation;
using MediatR;
using ValidationException = Products.Application.Common.Exceptions.ValidationException;

namespace Products.Application.Common.Behaviours;

/// <summary>
/// Runs every FluentValidation validator registered for a request before the
/// handler executes.
/// </summary>
/// <remarks>
/// Putting validation in the pipeline rather than at the top of each handler
/// means it cannot be forgotten when a new command is added: registering a
/// validator is enough to enforce it. Handlers are then free to assume their
/// input is well-formed and contain only business logic.
/// <para>
/// Validators run concurrently and <em>all</em> failures are collected, so the
/// client sees every problem with their payload at once rather than fixing them
/// one round-trip at a time.
/// </para>
/// </remarks>
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
