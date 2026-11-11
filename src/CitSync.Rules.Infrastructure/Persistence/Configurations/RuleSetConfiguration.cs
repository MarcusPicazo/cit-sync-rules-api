using CitSync.Rules.Domain.RuleSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitSync.Rules.Infrastructure.Persistence.Configurations;

internal sealed class RuleSetConfiguration : IEntityTypeConfiguration<RuleSet>
{
    public void Configure(EntityTypeBuilder<RuleSet> builder)
    {
        builder.ToTable("RuleSets");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(RuleSet.NameMaxLength).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(RuleSet.DescriptionMaxLength);
        builder.Property(r => r.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ScopeKey).HasMaxLength(RuleSet.ScopeKeyMaxLength).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(RuleSetVersion.ActorMaxLength).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => r.Name).IsUnique();

        // One rule set per scope. ScopeKey is never NULL ("school" for the School scope) on purpose:
        // EF adds "WHERE ScopeKey IS NOT NULL" to unique indexes on nullable columns, which would
        // allow many School rule sets.
        builder.HasIndex(r => new { r.Scope, r.ScopeKey }).IsUnique();

        builder.HasMany(r => r.Versions)
            .WithOne()
            .HasForeignKey(v => v.RuleSetId)
            .OnDelete(DeleteBehavior.Restrict);

        // EF writes to the private List<RuleSetVersion> _versions; the public property is read-only.
        builder.Navigation(r => r.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RuleSetVersionConfiguration : IEntityTypeConfiguration<RuleSetVersion>
{
    public void Configure(EntityTypeBuilder<RuleSetVersion> builder)
    {
        builder.ToTable("RuleSetVersions");
        builder.HasKey(v => v.Id);

        // IMPORTANT: Guid keys are "generated on add" by convention. A version added through the
        // RuleSet._versions list (not DbSet.Add) already has an Id, so EF would think it EXISTS and
        // issue an UPDATE (0 rows → concurrency exception). ValueGeneratedNever = "the app sets it".
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.CreatedBy).HasMaxLength(RuleSetVersion.ActorMaxLength).IsRequired();
        builder.Property(v => v.SubmittedBy).HasMaxLength(RuleSetVersion.ActorMaxLength);
        builder.Property(v => v.ReviewedBy).HasMaxLength(RuleSetVersion.ActorMaxLength);
        builder.Property(v => v.ReviewComment).HasMaxLength(RuleSetVersion.CommentMaxLength);
        builder.Property(v => v.RowVersion).IsRowVersion();

        builder.HasIndex(v => new { v.RuleSetId, v.Number }).IsUnique();

        // Rules are owned: they live and die with their version, in their own table.
        builder.OwnsMany(v => v.Rules, rules =>
        {
            rules.ToTable("RuleSetVersionRules");
            rules.WithOwner().HasForeignKey("RuleSetVersionId");
            rules.Property<int>("Id");
            rules.HasKey("Id");
            rules.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
            rules.Property(r => r.Parameters).IsRequired(); // nvarchar(max) holding a JSON object
        });
        builder.Navigation(v => v.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
