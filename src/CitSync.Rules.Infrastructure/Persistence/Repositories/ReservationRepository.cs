using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace CitSync.Rules.Infrastructure.Persistence.Repositories;

internal sealed class ReservationRepository(RulesDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reservations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PagedResult<Reservation>> ListAsync(
        ListReservationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Build the query step by step; EF translates the whole chain into ONE SQL statement.
        var source = db.Reservations.AsNoTracking();

        if (query.ResourceId is { } resourceId)
        {
            source = source.Where(r => r.ResourceId == resourceId);
        }

        if (query.Status is { } status)
        {
            source = source.Where(r => r.Status == status);
        }

        // Window filter = "overlaps [From, To)", same shape the NoOverlap rule will use.
        if (query.From is { } from)
        {
            var fromUtc = from.UtcDateTime;
            source = source.Where(r => r.EndUtc > fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.UtcDateTime;
            source = source.Where(r => r.StartUtc < toUtc);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(r => r.StartUtc)
            .ThenBy(r => r.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Reservation>(items, query.Page, query.PageSize, total);
    }

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}
