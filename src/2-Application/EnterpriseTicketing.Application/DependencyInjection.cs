using System.Reflection;
using EnterpriseTicketing.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseTicketing.Application;

/// <summary>
/// Application layer DI registration.
/// All application-layer registrations are encapsulated here.
/// The API layer calls AddApplication() and never references individual Application types directly.
/// This keeps the layer boundary explicit and testable.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR: auto-discovers all IRequestHandler implementations
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Pipeline behavior registration order matters — they execute in registration order (outermost first)
            // 1. UnhandledExceptionBehavior — outermost, catches everything
            // 2. ValidationBehavior — validates before handler
            // 3. PerformanceBehavior — measures actual handler time (inside validation)
            // 4. LoggingBehavior — innermost, closest to handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        // FluentValidation: auto-discovers all AbstractValidator<T> implementations
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
