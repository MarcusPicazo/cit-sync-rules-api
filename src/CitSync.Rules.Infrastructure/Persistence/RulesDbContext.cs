using CitSync.Rules.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence;

public sealed class RulesDbContext(DbContextOptions<RulesDbContext> options) : DbContext(options)
{
    public const string Schema = "rules";

    public DbSet<Resource> Resources => Set<Resource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RulesDbContext).Assembly);
    }
}
