using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CitSync.Rules.Infrastructure.Persistence;

/// <summary>
/// SQL Server datetime2 has no notion of time zone, so EF reads values back as
/// DateTimeKind.Unspecified. Everything we store is UTC, so we stamp the Kind on the way out.
/// Without this, serializing a reservation would drop the trailing "Z" and clients would
/// interpret the time as local.
/// </summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        toDb => toDb.Kind == DateTimeKind.Local ? toDb.ToUniversalTime() : toDb,
        fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));
