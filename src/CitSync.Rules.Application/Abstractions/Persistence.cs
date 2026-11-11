using CitSync.Rules.Application.Auditing;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.Auditing;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Abstractions;

// Ports: Application says WHAT it needs from storage; Infrastructure (EF Core) says HOW.

/// <summary>
/// Commits every change tracked in the current request in one transaction.
/// Implemented by the EF Core DbContext.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IResourceRepository
{
    /// <summary>Read-only (AsNoTracking).</summary>
    Task<Resource?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    Task<PagedResult<Resource>> ListAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(Resource resource);
}

public interface IReservationRepository
{
    /// <summary>Tracked: use it when you are going to change the reservation.</summary>
    Task<Reservation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only (AsNoTracking).</summary>
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Reservation>> ListAsync(ListReservationsQuery query, CancellationToken cancellationToken);

    void Add(Reservation reservation);
}

public interface IRuleSetRepository
{
    /// <summary>Tracked, with all versions loaded: the whole aggregate, ready to change.</summary>
    Task<RuleSet?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only (AsNoTracking), with versions.</summary>
    Task<RuleSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only, without versions (summary rows).</summary>
    Task<PagedResult<RuleSet>> ListAsync(ListRuleSetsQuery query, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    Task<bool> ScopeExistsAsync(RuleScope scope, string scopeKey, CancellationToken cancellationToken);

    void Add(RuleSet ruleSet);
}

public interface IAuditLogReader
{
    Task<PagedResult<AuditEntry>> ListAsync(AuditQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Who is making the request. Today it comes from the X-Actor header; later from the JWT.
/// Used for "CreatedBy"/"ReviewedBy" fields and by the audit interceptor.
/// </summary>
public interface ICurrentActor
{
    string Name { get; }
}
