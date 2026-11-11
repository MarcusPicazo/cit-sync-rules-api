using System.Text.Json;
using System.Text.Json.Serialization;
using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CitSync.Rules.Infrastructure.Persistence.Auditing;

/// <summary>
/// Runs right before EF sends the changes to SQL Server. For every entity that is being
/// created, updated or deleted it adds an AuditEntry to the SAME SaveChanges, so the change and
/// its audit row are committed in the same transaction: either both exist or neither does.
/// No service has to remember to audit.
/// </summary>
internal sealed class AuditSaveChangesInterceptor(ICurrentActor actor, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // The interceptor runs before EF's own DetectChanges, so make sure the tracker is up to date.
        context.ChangeTracker.DetectChanges();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var who = actor.Name;

        // ToList: we are about to add AuditEntry rows to the same tracker we are iterating.
        var changes = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry
                && !e.Metadata.IsOwned() // owned rules are reported inside their version
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in changes)
        {
            var audit = entry.State switch
            {
                EntityState.Added => new AuditEntry(
                    EntityName(entry), EntityKey(entry), AuditAction.Created, who, now,
                    before: null, after: Serialize(Snapshot(entry, onlyModified: false, useOriginal: false, withOwnedOf: entry))),

                EntityState.Deleted => new AuditEntry(
                    EntityName(entry), EntityKey(entry), AuditAction.Deleted, who, now,
                    before: Serialize(Snapshot(entry, onlyModified: false, useOriginal: true)), after: null),

                _ => CreateUpdateEntry(entry, who, now),
            };

            if (audit is not null)
            {
                context.Add(audit);
            }
        }
    }

    private static AuditEntry? CreateUpdateEntry(EntityEntry entry, string who, DateTime now)
    {
        var before = Snapshot(entry, onlyModified: true, useOriginal: true);
        if (before.Count == 0)
        {
            return null; // only the rowversion or nothing real changed
        }

        var after = Snapshot(entry, onlyModified: true, useOriginal: false);
        return new AuditEntry(
            EntityName(entry), EntityKey(entry), ResolveUpdateAction(entry), who, now,
            Serialize(before), Serialize(after));
    }

    /// <summary>
    /// A Modified entry is normally an "Updated" action. Soft deletes are Modified too
    /// (IsDeleted false → true), but for whoever reads the audit trail they are deletions.
    /// </summary>
    private static AuditAction ResolveUpdateAction(EntityEntry entry)
    {
        // TODO(Marcus): return AuditAction.Deleted when this update is a soft delete.
        //   - The entity type may or may not have an "IsDeleted" property: use
        //     entry.Metadata.FindProperty("IsDeleted") to check without throwing.
        //   - It is a soft delete only if that property IS MODIFIED in this save, its original
        //     value was false and its current value is true (look at entry.Property(...).IsModified,
        //     .OriginalValue and .CurrentValue; they are object?, so think about how to compare them).
        // Then remove the Skip from Soft_delete_is_audited_as_Deleted in IntegrationTests/AuditEndpointsTests.cs.
        _ = entry;
        return AuditAction.Updated;
    }

    private static Dictionary<string, object?> Snapshot(
        EntityEntry entry, bool onlyModified, bool useOriginal, EntityEntry? withOwnedOf = null)
    {
        var values = entry.Properties
            .Where(p => !p.Metadata.IsConcurrencyToken) // the rowversion is noise in an audit trail
            .Where(p => !onlyModified || p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => useOriginal ? p.OriginalValue : p.CurrentValue);

        // For a newly created entity, include its owned collections (e.g. the rules of a version).
        if (withOwnedOf is not null)
        {
            foreach (var navigation in withOwnedOf.Navigations
                         .Where(n => n.Metadata.TargetEntityType.IsOwned() && n.CurrentValue is not null))
            {
                values[navigation.Metadata.Name] = navigation.CurrentValue;
            }
        }

        return values;
    }

    private static string EntityName(EntityEntry entry) => entry.Metadata.ClrType.Name;

    private static string EntityKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{EntityName(entry)} has no primary key.");
        return string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string Serialize(Dictionary<string, object?> values) => JsonSerializer.Serialize(values, Json);
}
