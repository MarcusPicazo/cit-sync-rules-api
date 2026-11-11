using CitSync.Rules.Application.Auditing;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Application.Rules.Evaluators;
using CitSync.Rules.Application.RuleSets;
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
        services.AddScoped<IRuleSetService, RuleSetService>();
        services.AddScoped<IAuditService, AuditService>();

        // Rule engine (strategy pattern): one IRuleEvaluator per RuleType. The engine receives
        // IEnumerable<IRuleEvaluator> and indexes them by type. A new rule = one class + one line here.
        // Scoped because NoOverlap uses the (scoped) reservation repository.
        services.AddScoped<IRuleEvaluator, OpeningHoursEvaluator>();
        services.AddScoped<IRuleEvaluator, MaxDurationEvaluator>();
        services.AddScoped<IRuleEvaluator, NoOverlapEvaluator>();
        services.AddScoped<IRuleEvaluator, RoleRestrictionEvaluator>();
        services.AddScoped<IRuleEvaluator, BlackoutDatesEvaluator>();
        services.AddScoped<IRuleEvaluator, LeadTimeEvaluator>();

        // One engine instance per request, exposed through two narrow interfaces (ISP).
        services.AddScoped<RuleEngine>();
        services.AddScoped<IRuleEngine>(sp => sp.GetRequiredService<RuleEngine>());
        services.AddScoped<IRuleParametersValidator>(sp => sp.GetRequiredService<RuleEngine>());

        return services;
    }
}
