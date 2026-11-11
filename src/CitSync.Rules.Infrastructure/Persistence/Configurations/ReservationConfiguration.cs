using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitSync.Rules.Infrastructure.Persistence.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public const string ActiveByResourceAndTimeIndex = "IX_Reservations_ResourceId_StartUtc_EndUtc_Active";

    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", t =>
        {
            t.HasCheckConstraint("CK_Reservations_Quantity", "[Quantity] >= 1");
            t.HasCheckConstraint("CK_Reservations_Range", "[EndUtc] > [StartUtc]");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequesterId).HasMaxLength(Reservation.RequesterIdMaxLength).IsRequired();
        builder.Property(r => r.Subject).HasMaxLength(Reservation.SubjectMaxLength).IsRequired();
        builder.Property(r => r.StatusReason).HasMaxLength(Reservation.StatusReasonMaxLength);
        builder.Property(r => r.RequesterRole).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(r => r.Duration);

        // rowversion: SQL Server bumps it on every UPDATE; EF adds "AND RowVersion = @original"
        // to the UPDATE and throws DbUpdateConcurrencyException if 0 rows match.
        builder.Property(r => r.RowVersion).IsRowVersion();

        // FK without a navigation property: Domain stays simple, the DB still guarantees integrity.
        builder.HasOne<Resource>()
            .WithMany()
            .HasForeignKey(r => r.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete: every LINQ query on Reservations gets "WHERE IsDeleted = 0" automatically.
        // IgnoreQueryFilters() opts out (e.g. for the audit screen).
        builder.HasQueryFilter(r => !r.IsDeleted);

        // Supports the overlap query of the NoOverlap rule (phase 4). Filtered so cancelled
        // and deleted rows don't bloat it. Status/Quantity are INCLUDEd so the query is covered.
        builder.HasIndex(r => new { r.ResourceId, r.StartUtc, r.EndUtc })
            .HasDatabaseName(ActiveByResourceAndTimeIndex)
            .HasFilter("[IsDeleted] = 0 AND [Status] <> 'Cancelled'")
            .IncludeProperties(r => new { r.Status, r.Quantity });

        builder.HasIndex(r => r.Status);
    }
}
