using Microsoft.Extensions.DependencyInjection;

namespace CitSync.Rules.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers use cases, validators and the rule engine (filled in from phase 2 on).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
