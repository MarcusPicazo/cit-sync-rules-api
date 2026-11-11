using CitSync.Rules.Domain.Common;
using CitSync.Rules.Domain.Reservations;
using FluentAssertions;

namespace CitSync.Rules.UnitTests.Domain;

public class ReservationTests
{
    private static readonly DateTime Start = new(2030, 1, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Created = new(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Reservation NewReservation(DateTime? end = null) => new(
        Guid.NewGuid(),
        " A01234 ",
        RequesterRole.Alumno,
        Start,
        end ?? Start.AddHours(2),
        quantity: 2,
        " Robótica ",
        Created);

    [Fact]
    public void Constructor_creates_a_pending_reservation_with_trimmed_values()
    {
        var reservation = NewReservation();

        reservation.Id.Should().NotBeEmpty();
        reservation.Status.Should().Be(ReservationStatus.Pending);
        reservation.RequesterId.Should().Be("A01234");
        reservation.Subject.Should().Be("Robótica");
        reservation.Duration.Should().Be(TimeSpan.FromHours(2));
        reservation.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_end_before_or_equal_to_start()
    {
        var act = () => NewReservation(end: Start);

        act.Should().Throw<ArgumentException>().WithParameterName("endUtc");
    }

    [Fact]
    public void Constructor_rejects_non_utc_times()
    {
        var local = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);

        var act = () => new Reservation(
            Guid.NewGuid(), "A01234", RequesterRole.Alumno, local, local.AddHours(1), 1, "Física", Created);

        act.Should().Throw<ArgumentException>().WithParameterName("startUtc");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_quantity(int quantity)
    {
        var act = () => new Reservation(
            Guid.NewGuid(), "A01234", RequesterRole.Alumno, Start, Start.AddHours(1), quantity, "Física", Created);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Fact]
    public void Approve_moves_pending_to_approved()
    {
        var reservation = NewReservation();

        reservation.Approve();

        reservation.Status.Should().Be(ReservationStatus.Approved);
    }

    [Fact]
    public void Approve_twice_throws_domain_exception()
    {
        var reservation = NewReservation();
        reservation.Approve();

        var act = reservation.Approve;

        act.Should().Throw<DomainException>().WithMessage("*Approved*");
    }

    [Fact]
    public void Reject_stores_trimmed_reason()
    {
        var reservation = NewReservation();

        reservation.Reject("  Laboratorio en mantenimiento ");

        reservation.Status.Should().Be(ReservationStatus.Rejected);
        reservation.StatusReason.Should().Be("Laboratorio en mantenimiento");
    }

    [Fact]
    public void Reject_after_approve_throws_domain_exception()
    {
        var reservation = NewReservation();
        reservation.Approve();

        var act = () => reservation.Reject("tarde");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SoftDelete_marks_as_deleted_once()
    {
        var reservation = NewReservation();
        var first = Created.AddDays(1);

        reservation.SoftDelete(first);
        reservation.SoftDelete(first.AddDays(1));

        reservation.IsDeleted.Should().BeTrue();
        reservation.DeletedUtc.Should().Be(first);
    }

    [Fact]
    public void Cancel_from_pending_sets_cancelled_and_reason()
    {
        var reservation = NewReservation();

        reservation.Cancel(" Se suspendió la clase ");

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.StatusReason.Should().Be("Se suspendió la clase");
    }

    [Fact]
    public void Cancel_from_approved_is_allowed()
    {
        var reservation = NewReservation();
        reservation.Approve();

        reservation.Cancel(null);

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Cancel_from_rejected_or_cancelled_throws_domain_exception()
    {
        var rejected = NewReservation();
        rejected.Reject(null);

        var actOnRejected = () => rejected.Cancel(null);

        actOnRejected.Should().Throw<DomainException>().WithMessage("*Rejected*");

        var cancelled = NewReservation();
        cancelled.Cancel(null);

        var actOnCancelled = () => cancelled.Cancel(null);

        actOnCancelled.Should().Throw<DomainException>().WithMessage("*Cancelled*");
    }
}
