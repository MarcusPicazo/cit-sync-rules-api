# Query tuning: the NoOverlap overlap query

The `NoOverlap` rule asks: *how many units of this resource are already in use during the
requested window?* It runs on every `POST /api/v1/rules/evaluate`, so it must stay cheap as the
reservation history grows.

## The query

EF Core generates this SQL, captured from the API log. It is sent with `sp_executesql` and typed
parameters:

```sql
SELECT [r].[StartUtc], [r].[EndUtc], [r].[Quantity]
FROM [rules].[Reservations] AS [r]
WHERE [r].[IsDeleted] = CAST(0 AS bit)          -- global query filter (soft delete)
  AND [r].[ResourceId] = @resourceId            -- equality on the 1st index key
  AND [r].[StartUtc] < @endUtc                  -- range on the 2nd index key  ┐ overlap of
  AND [r].[EndUtc]   > @startUtc                -- residual, read from the key ┘ half-open intervals
  AND [r].[Status] <> N'Cancelled'              -- repeats the index filter (defensive, see B)
  AND [r].[Status] IN (N'Approved', N'Pending') -- statuses that occupy capacity
```

Source: `ReservationRepository.GetOccupiedSlotsAsync`.

**Two intervals `[a, b)` and `[c, d)` overlap when `a < d AND c < b`.** Using strict `<` makes
back-to-back bookings (9–10 and 10–11) *not* overlap, which is what a school expects.

### Why it is sargable

*Sargable* means "Search ARGument ABLE": every predicate compares a **bare column** against a
value, so SQL Server can **seek** the index instead of scanning it.

| Do | Don't |
|---|---|
| `StartUtc < @endUtc` | `DATEADD(MINUTE, 0, StartUtc) < @endUtc` |
| `ResourceId = @id` | `CONVERT(varchar(36), ResourceId) = @idText` |
| compute values on the **parameter** side | wrap the **column** in a function or cast |

## The index (Phase 2 migration)

```sql
CREATE INDEX IX_Reservations_ResourceId_StartUtc_EndUtc_Active
    ON rules.Reservations (ResourceId, StartUtc, EndUtc)
    INCLUDE (Status, Quantity)
    WHERE [IsDeleted] = 0 AND [Status] <> 'Cancelled';
```

- **Key order:** the equality column goes first (`ResourceId`), then the range column (`StartUtc`).
  The seek jumps straight to "this resource, starting before `@endUtc`".
- **`INCLUDE (Status, Quantity)`:** together with the key columns, every column the query reads or
  filters is in the index. The index **covers** the query, so there is no *Key Lookup* to the table.
- **Filtered:** soft-deleted and cancelled rows never enter the index. They are dead weight for
  this query, and they would grow forever.

Gotchas we hit with the filtered index:

1. **`SET QUOTED_IDENTIFIER ON` is mandatory to write to the table.** Without it, any
   INSERT/UPDATE fails with *Msg 1934*. EF Core and ADO.NET set it by default. `sqlcmd` does not,
   so scripts must set it (see `docs/perf/seed-reservations.sql`).
2. **The optimizer only uses a filtered index when it can prove the query implies the filter.**
   We repeat `Status <> 'Cancelled'` literally in the query. Measurements (variant B) show that
   SQL Server can also prove it from `IN ('Approved','Pending')`, so the repetition is defensive:
   it keeps the index usable if someone changes the status list.

## Measurements

| Setup | Value |
|---|---|
| Server | SQL Server 2022 Developer (Docker) |
| Data | 100,000 fictional reservations: 200 resources × 500 one-hour bookings over ~2 years |
| Status mix | 10% Cancelled, 10% Rejected, 10% Pending, rest Approved; 4% soft-deleted |
| Script | `docs/perf/overlap-plans.sql` |

The script runs each variant through `sp_executesql` with EF's parameter types, records
`STATISTICS IO`, and reads the operator from the cached plan.

| Variant | Operator / index | Logical reads, window early in history (2029-06) | Logical reads, window late in history (2030-12) |
|---|---|---|---|
| **A** — production query | Index Seek, filtered index | **6** | **10** |
| **B** — A without `Status <> 'Cancelled'` | Index Seek, filtered index | 6 | 10 |
| **C** — function on the column (non-sargable) | Index Seek on `ResourceId` only, range lost | 11 | 11 |
| **D** — A + lower bound `StartUtc > @startUtc - 24h` | Index Seek, filtered index | **4** | **3** |
| **E** — A forced onto the clustered index | Clustered Index **Scan** | 2,290 | 2,290 |

### What these numbers say

- **The index pays off.** The seek reads 6–10 pages; scanning the table reads 2,290, about
  230–380× more. The scan gets worse with every new reservation in the system. The seek only
  depends on the history of **one** resource.
- **Non-sargable code fails quietly.** In C, SQL Server can still seek on `ResourceId`, the
  leading key column, so the plan "looks fine". It just reads every row of that resource instead
  of a range. Here that is 11 pages, but it grows linearly with the resource's history.
- **A has one weakness: it is bounded on one side only.** `StartUtc < @endUtc` reads every
  booking of the resource that starts **before** the window, back to the beginning of time; only
  then does `EndUtc > @startUtc` discard most of them. Reads grow from 6 to 10 as the window moves
  later in history (A, early vs. late).
- **D fixes that** (3–4 reads, flat) by adding a lower bound. That is only correct if a
  reservation can never be longer than the bound (24 h here). Today nothing enforces a maximum
  length (only the optional `MaxDuration` rule), so D is **not** in production.
  **Recommendation:** add a domain invariant such as "a reservation lasts at most 24 h" and then
  adopt D.
- **Latency:** after the first call, the query takes **3–4 ms** end to end in the EF log, network
  round trip to the container included. The first call took 522 ms: plan compilation plus a cold
  buffer pool right after loading the data.
- **Parameter sniffing:** the estimated row count (61.7) is identical for both windows, because the
  second run **reused the cached plan** compiled for the first window's parameters. With this index
  the plan shape (seek) is right for any window, so sniffing is harmless here. Keep it in mind if
  the plan ever flips to a scan for some resources.

## Why the peak and not the sum

The query returns the overlapping rows. The capacity check then computes the **peak** concurrent
quantity in memory (`CapacityMath.PeakConcurrentQuantity`, a sweep line). `SUM(Quantity)` would
over-count. For example, 9–10 (2 units) and 10–11 (2 units) both overlap a 9–11 request, but at
most 2 units are ever in use at the same time, not 4. Doing this in SQL is possible with window
functions, but it is harder to read. The row count per call is small: only the bookings of one
resource that overlap one window.

## Reproduce

```bash
# 1. fictional load (idempotent) — local dev database only
docker cp docs/perf/seed-reservations.sql cit-sync-rules-sql:/tmp/seed.sql
docker exec cit-sync-rules-sql /bin/bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d CitSyncRules -i /tmp/seed.sql'

# 2. compare plans for a window
docker cp docs/perf/overlap-plans.sql cit-sync-rules-sql:/tmp/plans.sql
docker exec cit-sync-rules-sql /bin/bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d CitSyncRules -v WinStart=2030-12-01T14:00:00 WinEnd=2030-12-01T16:00:00 -i /tmp/plans.sql'

# 3. remove the fictional load
docker cp docs/perf/cleanup.sql cit-sync-rules-sql:/tmp/cleanup.sql
docker exec cit-sync-rules-sql /bin/bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d CitSyncRules -i /tmp/cleanup.sql'
```

In Git Bash on Windows, run `export MSYS_NO_PATHCONV=1` first. Otherwise `/bin/bash` and
`/tmp/...` get rewritten as Windows paths.
