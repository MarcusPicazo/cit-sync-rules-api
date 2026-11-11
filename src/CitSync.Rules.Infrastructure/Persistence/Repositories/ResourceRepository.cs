using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence.Repositories;

internal sealed class ResourceRepository(RulesDbContext db) : IResourceRepository
{
    public Task<Resource?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        db.Resources.AnyAsync(r => r.Name == name, cancellationToken);

    public async Task<PagedResult<Resource>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var source = db.Resources.AsNoTracking();

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id) // stable order, otherwise pages can repeat/skip rows
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Resource>(items, query.Page, query.PageSize, total);
    }

    public void Add(Resource resource) => db.Resources.Add(resource);
}
