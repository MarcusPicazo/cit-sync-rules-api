using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;

namespace CitSync.Rules.UnitTests.Rules;

internal static class RuleTestData
{
    /// <summary>Mexico City is UTC-6 all year (no DST since 2022).</summary>
    public static readonly TimeSpan SchoolOffset = TimeSpan.FromHours(-6);

    /// <summary>Monday 2030-01-14, 12:00 UTC (06:00 school time).</summary>
    public static readonly DateTime NowUtc = new(2030, 1, 14, 12, 0, 0, DateTimeKind.Utc);

    public static Resource Lab(int capacity = 10) =>
        new("Laboratorio de Robótica", ResourceCategory.Lab, capacity, "Edificio A");

    /// <summary>Builds a context from SCHOOL-LOCAL times, which is how people think about bookings.</summary>
    public static RuleContext Context(
        DateTime localStart,
        DateTime localEnd,
        RequesterRole role = RequesterRole.Alumno,
        int quantity = 1,
        Resource? resource = null,
        DateTime? nowUtc = null) => new()
    {
        Resource = resource ?? Lab(),
        RequesterRole = role,
        StartUtc = new DateTimeOffset(localStart, SchoolOffset).UtcDateTime,
        EndUtc = new DateTimeOffset(localEnd, SchoolOffset).UtcDateTime,
        Quantity = quantity,
        NowUtc = nowUtc ?? NowUtc,
    };

    /// <summary>Monday 2030-01-21 at the given school-local hour.</summary>
    public static DateTime Monday(int hour, int minute = 0) => new(2030, 1, 21, hour, minute, 0);
}
