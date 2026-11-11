using CitSync.Rules.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitSync.Rules.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn(); // bigint IDENTITY: append-only log

        builder.Property(a => a.Entity).HasMaxLength(AuditEntry.EntityMaxLength).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength).IsRequired();
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Actor).HasMaxLength(AuditEntry.ActorMaxLength).IsRequired();

        // Typical query: "history of this entity, newest first".
        builder.HasIndex(a => new { a.Entity, a.EntityId, a.TimestampUtc });
    }
}
