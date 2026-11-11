using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.Resources;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CitSync.Rules.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers use cases, validators and (from phase 4 on) the rule engine.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scans this assembly for every AbstractValidator<T> and registers it as IValidator<T>.
        // Validators are stateless, so one instance for the whole app is enough.
        services.AddValidatorsFromAssemblyContaining<CreateResourceRequestValidator>(ServiceLifetime.Singleton);

        // Abstract clock: production uses the real one, tests can inject a fixed time.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<IReservationService, ReservationService>();

        return services;
    }
}
