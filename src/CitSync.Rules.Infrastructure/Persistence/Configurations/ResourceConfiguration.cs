using CitSync.Rules.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitSync.Rules.Infrastructure.Persistence.Configurations;

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(Resource.NameMaxLength).IsRequired();
        builder.Property(r => r.Building).HasMaxLength(Resource.BuildingMaxLength);
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(r => r.Name).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("CK_Resources_Capacity", "[Capacity] >= 1"));
    }
}
