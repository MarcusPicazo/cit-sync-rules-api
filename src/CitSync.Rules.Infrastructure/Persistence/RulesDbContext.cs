using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string Schema = "rules";

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Applies to every DateTime (and DateTime?) property in the model.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
