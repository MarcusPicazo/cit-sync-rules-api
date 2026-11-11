using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Auditing;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogReader(RulesDbContext db) : IAuditLogReader
{
    public async Task<PagedResult<AuditEntry>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            source = source.Where(a => a.Entity == query.Entity);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            source = source.Where(a => a.EntityId == query.EntityId);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(a => a.Id) // identity = insertion order, cheaper and stabler than the timestamp
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntry>(items, query.Page, query.PageSize, total);
    }
}
