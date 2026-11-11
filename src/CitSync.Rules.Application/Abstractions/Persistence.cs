using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;

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
