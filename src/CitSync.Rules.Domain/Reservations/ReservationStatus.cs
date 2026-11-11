namespace CitSync.Rules.Domain.Reservations;

/// <summary>
/// Pending → Approved | Rejected | Cancelled. Approved → Cancelled.
/// </summary>
public enum ReservationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}
