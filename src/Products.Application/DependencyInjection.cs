using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Products.Application.Common.Behaviours;

namespace Products.Application;

/// <summary>
/// Registers the Application layer with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Adds MediatR, the pipeline behaviours and all FluentValidation validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Order is significant, behaviours nest in registration order:
            //   Logging  ( Performance ( Validation ( handler ) ) )
            // Logging is outermost so a validation failure is still attributed to
            // its request. Validation is innermost so the handler never runs on
            // invalid input, and so the timer does not count rejected requests.
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // TimeProvider is the built-in clock abstraction (.NET 8+). Injecting it
        // rather than calling DateTimeOffset.UtcNow directly is what lets the
        // tests assert exact timestamps. Registered here so every handler that
        // needs a clock resolves the same one.
        services.TryAddSingletonTimeProvider();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
