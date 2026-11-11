using System.Text.Json;
using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Auditing;
using FluentValidation;

namespace CitSync.Rules.Application.Auditing;

public sealed class AuditQuery : PageQuery
{
    /// <summary>Entity type name, e.g. "Reservation" or "RuleSetVersion".</summary>
    public string? Entity { get; init; }

    public string? EntityId { get; init; }
}

public sealed record AuditEntryResponse(
    long Id,
    string Entity,
    string EntityId,
    AuditAction Action,
    string Actor,
    DateTime TimestampUtc,
    JsonElement? Before,
    JsonElement? After)
{
    public static AuditEntryResponse From(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new(
            entry.Id,
            entry.Entity,
            entry.EntityId,
            entry.Action,
            entry.Actor,
            entry.TimestampUtc,
            JsonText.ToElementOrNull(entry.Before),
            JsonText.ToElementOrNull(entry.After));
    }
}

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(q => q.Entity).MaximumLength(AuditEntry.EntityMaxLength);
        RuleFor(q => q.EntityId).MaximumLength(AuditEntry.EntityIdMaxLength);
    }
}

public interface IAuditService
{
    Task<PagedResult<AuditEntryResponse>> ListAsync(AuditQuery query, CancellationToken cancellationToken);
}

public sealed class AuditService(IAuditLogReader auditLog, IValidator<AuditQuery> validator) : IAuditService
{
    public async Task<PagedResult<AuditEntryResponse>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var page = await auditLog.ListAsync(query, cancellationToken);
        return page.Map(AuditEntryResponse.From);
    }
}
