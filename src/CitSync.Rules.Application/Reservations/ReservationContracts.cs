using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Reservations;

namespace CitSync.Rules.Application.Reservations;

/// <summary>
/// Start/End are DateTimeOffset so the client must say which offset it means
/// ("2026-11-20T09:00:00-06:00"); we normalize to UTC before storing.
/// </summary>
public sealed record CreateReservationRequest(
    Guid ResourceId,
    string RequesterId,
    RequesterRole RequesterRole,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Quantity,
    string Subject);

public sealed record ChangeReservationStatusRequest(string? Reason);

public sealed class ListReservationsQuery : PageQuery
{
    public Guid? ResourceId { get; init; }
    public ReservationStatus? Status { get; init; }

    /// <summary>Only reservations that end after this instant.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only reservations that start before this instant.</summary>
    public DateTimeOffset? To { get; init; }
}

public sealed record ReservationResponse(
    Guid Id,
    Guid ResourceId,
    string RequesterId,
    RequesterRole RequesterRole,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTimeOffset StartLocal,
    DateTimeOffset EndLocal,
    string TimeZone,
    int Quantity,
    string Subject,
    ReservationStatus Status,
    string? StatusReason,
    DateTime CreatedUtc)
{
    public static ReservationResponse From(Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return new(
            reservation.Id,
            reservation.ResourceId,
            reservation.RequesterId,
            reservation.RequesterRole,
            reservation.StartUtc,
            reservation.EndUtc,
            SchoolTimeZone.ToSchoolTime(reservation.StartUtc),
            SchoolTimeZone.ToSchoolTime(reservation.EndUtc),
            SchoolTimeZone.Id,
            reservation.Quantity,
            reservation.Subject,
            reservation.Status,
            reservation.StatusReason,
            reservation.CreatedUtc);
    }
}
