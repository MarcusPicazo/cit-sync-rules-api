using CitSync.Rules.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CitSync.Rules.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "RulesDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Comes from user-secrets in development and from the environment
        // (ConnectionStrings__RulesDb) everywhere else. Never from appsettings.json.
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing. " +
                "Set it with 'dotnet user-secrets' or the ConnectionStrings__RulesDb environment variable.");

        services.AddDbContext<RulesDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", RulesDbContext.Schema);
                sql.EnableRetryOnFailure();
            }));

        return services;
    }
}
