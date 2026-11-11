using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.RuleSets;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence.Repositories;

internal sealed class RuleSetRepository(RulesDbContext db) : IRuleSetRepository
{
    // Owned rules are loaded automatically with each version; versions need an explicit Include.
    public Task<RuleSet?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.RuleSets.Include(r => r.Versions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<RuleSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.RuleSets.AsNoTracking().Include(r => r.Versions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PagedResult<RuleSet>> ListAsync(ListRuleSetsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.RuleSets.AsNoTracking();

        if (query.Scope is { } scope)
        {
            source = source.Where(r => r.Scope == scope);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RuleSet>(items, query.Page, query.PageSize, total);
    }

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        db.RuleSets.AnyAsync(r => r.Name == name, cancellationToken);

    public Task<bool> ScopeExistsAsync(RuleScope scope, string scopeKey, CancellationToken cancellationToken) =>
        db.RuleSets.AnyAsync(r => r.Scope == scope && r.ScopeKey == scopeKey, cancellationToken);

    public void Add(RuleSet ruleSet) => db.RuleSets.Add(ruleSet);
}
