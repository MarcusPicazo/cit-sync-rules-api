using CitSync.Rules.Domain.Common;

namespace CitSync.Rules.Domain.Reservations;

/// <summary>
/// A request to use a <see cref="Resources.Resource"/> during a time window.
/// All times are UTC; conversion to the school time zone happens at the edges (API responses).
/// </summary>
public sealed class Reservation
{
    public const int RequesterIdMaxLength = 40;
    public const int SubjectMaxLength = 120;
    public const int StatusReasonMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid ResourceId { get; private set; }

    /// <summary>Matrícula (alumnos) or número de empleado (profesores / admins).</summary>
    public string RequesterId { get; private set; }

    public RequesterRole RequesterRole { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public int Quantity { get; private set; }
    public string Subject { get; private set; }
    public ReservationStatus Status { get; private set; }

    /// <summary>Why it was rejected or cancelled, if anyone said.</summary>
    public string? StatusReason { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    // Soft delete only: rows are never physically removed (audit trail in phase 3).
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedUtc { get; private set; }

    public Reservation(
        Guid resourceId,
        string requesterId,
        RequesterRole requesterRole,
        DateTime startUtc,
        DateTime endUtc,
        int quantity,
        string subject,
        DateTime createdUtc)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id is required.", nameof(resourceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requesterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        EnsureUtc(startUtc, nameof(startUtc));
        EnsureUtc(endUtc, nameof(endUtc));
        EnsureUtc(createdUtc, nameof(createdUtc));

        if (endUtc <= startUtc)
        {
            throw new ArgumentException("End must be after start.", nameof(endUtc));
        }

        Id = Guid.NewGuid();
        ResourceId = resourceId;
        RequesterId = requesterId.Trim();
        RequesterRole = requesterRole;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Quantity = quantity;
        Subject = subject.Trim();
        Status = ReservationStatus.Pending;
        CreatedUtc = createdUtc;
    }

    // Used by EF Core when materializing rows, so the validations above don't run against stored data.
    private Reservation()
    {
        RequesterId = null!;
        Subject = null!;
    }

    public TimeSpan Duration => EndUtc - StartUtc;

    public void Approve()
    {
        EnsureStatus(ReservationStatus.Pending, "approve");
        Status = ReservationStatus.Approved;
        StatusReason = null;
    }

    public void Reject(string? reason)
    {
        EnsureStatus(ReservationStatus.Pending, "reject");
        Status = ReservationStatus.Rejected;
        StatusReason = NormalizeReason(reason);
    }

    public void Cancel(string? reason)
    {
        // Allowed from Pending and from Approved (a professor can cancel a class that was already approved).
        // From Rejected or Cancelled it is a business-rule violation -> DomainException -> 409 Conflict.
        if (Status is not (ReservationStatus.Pending or ReservationStatus.Approved))
        {
            throw new DomainException(
                $"Cannot cancel reservation {Id}: it is {Status}, it must be {ReservationStatus.Pending} or {ReservationStatus.Approved}.");
        }

        Status = ReservationStatus.Cancelled;
        StatusReason = NormalizeReason(reason);
    }

    public void SoftDelete(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedUtc = utcNow;
    }

    private void EnsureStatus(ReservationStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException(
                $"Cannot {action} reservation {Id}: it is {Status}, it must be {expected}.");
        }
    }

    private static string? NormalizeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date must be in UTC (DateTimeKind.Utc).", paramName);
        }
    }
}
