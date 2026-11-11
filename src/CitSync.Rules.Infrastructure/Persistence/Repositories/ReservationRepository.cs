using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.Rules;
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

    public async Task<IReadOnlyList<OccupiedSlot>> GetOccupiedSlotsAsync(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc,
        bool includePending,
        Guid? excludeReservationId,
        CancellationToken cancellationToken)
    {
        // Sargable overlap test: every predicate compares a bare COLUMN against a value, so SQL Server
        // can SEEK the index (ResourceId = @id, then range on StartUtc). Never wrap the column in a
        // function (DATEADD(StartUtc), CAST(...)) or the seek becomes a scan.
        var query = db.Reservations // the global query filter adds [IsDeleted] = 0
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId
                && r.StartUtc < endUtc
                && r.EndUtc > startUtc
                // Repeats the index filter literally ("Status <> 'Cancelled'"). SQL Server only uses a
                // filtered index when the query provably implies its filter. Measured: here it CAN infer
                // it from "Status IN ('Pending','Approved')" too (docs/query-tuning.md, variant B), so
                // this is defensive: it keeps the index usable if the status list ever changes.
                && r.Status != ReservationStatus.Cancelled);

        query = includePending
            ? query.Where(r => r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Pending)
            : query.Where(r => r.Status == ReservationStatus.Approved);

        if (excludeReservationId is { } excluded)
        {
            query = query.Where(r => r.Id != excluded);
        }

        // Project ONLY indexed columns (key: StartUtc, EndUtc; INCLUDE: Quantity, Status) so the index
        // covers the query and SQL Server never touches the table (no Key Lookup).
        return await query
            .Select(r => new OccupiedSlot(r.StartUtc, r.EndUtc, r.Quantity))
            .ToListAsync(cancellationToken);
    }

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);
}
