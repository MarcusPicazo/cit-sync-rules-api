namespace CitSync.Rules.Domain.Auditing;

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
}

/// <summary>
/// One change to one entity: who, when, and the values before/after (JSON).
/// Written automatically by the EF Core SaveChanges interceptor; never by hand.
/// </summary>
public sealed class AuditEntry
{
    public const int EntityMaxLength = 100;
    public const int EntityIdMaxLength = 100;
    public const int ActorMaxLength = 100;

    public long Id { get; private set; }
    public string Entity { get; private set; }
    public string EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public string Actor { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? Before { get; private set; }
    public string? After { get; private set; }

    public AuditEntry(
        string entity,
        string entityId,
        AuditAction action,
        string actor,
        DateTime timestampUtc,
        string? before,
        string? after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        Entity = entity;
        EntityId = entityId;
        Action = action;
        Actor = actor;
        TimestampUtc = timestampUtc;
        Before = before;
        After = after;
    }
}
