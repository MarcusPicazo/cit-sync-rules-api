using CitSync.Rules.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace CitSync.Rules.IntegrationTests;

/// <summary>
/// Boots the real API in memory against a throwaway SQL Server container.
/// </summary>
public sealed class RulesApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RulesDb", _sqlServer.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RulesDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlServer.DisposeAsync();
        await base.DisposeAsync();
    }
}
