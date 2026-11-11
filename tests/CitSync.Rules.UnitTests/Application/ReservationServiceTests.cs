using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Domain.Common;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using FluentAssertions;
using FluentValidation;
using NSubstitute;

namespace CitSync.Rules.UnitTests.Application;

public class ReservationServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);

    // 9:00–11:00 Mexico City (UTC-6) = 15:00–17:00 UTC.
    private static readonly DateTimeOffset LocalStart = new(2030, 1, 15, 9, 0, 0, TimeSpan.FromHours(-6));

    private readonly IReservationRepository _reservations = Substitute.For<IReservationRepository>();
    private readonly IResourceRepository _resources = Substitute.For<IResourceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();
    private readonly ReservationService _sut;

    public ReservationServiceTests()
    {
        _clock.GetUtcNow().Returns(Now);
        _sut = new ReservationService(
            _reservations,
            _resources,
            _unitOfWork,
            _clock,
            new ListReservationsQueryValidator(),
            new CreateReservationRequestValidator(),
            new ChangeReservationStatusRequestValidator());
    }

    private static CreateReservationRequest Request(Guid resourceId, int quantity = 1) => new(
        resourceId, "A01234", RequesterRole.Alumno, LocalStart, LocalStart.AddHours(2), quantity, "Robótica");

    private Resource GivenResource(int capacity = 10, bool active = true)
    {
        var resource = new Resource("Laboratorio de Robótica", ResourceCategory.Lab, capacity);
        if (!active)
        {
            resource.Deactivate();
        }

        _resources.GetByIdAsync(resource.Id, Arg.Any<CancellationToken>()).Returns(resource);
        return resource;
    }

    [Fact]
    public async Task Create_saves_a_pending_reservation_in_utc_and_returns_school_time()
    {
        var resource = GivenResource();

        var response = await _sut.CreateAsync(Request(resource.Id, quantity: 3), CancellationToken.None);

        response.Status.Should().Be(ReservationStatus.Pending);
        response.StartUtc.Should().Be(new DateTime(2030, 1, 15, 15, 0, 0, DateTimeKind.Utc));
        response.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.StartLocal.Should().Be(LocalStart);
        response.StartLocal.Offset.Should().Be(TimeSpan.FromHours(-6));
        response.CreatedUtc.Should().Be(Now.UtcDateTime);

        _reservations.Received(1).Add(Arg.Is<Reservation>(r => r.ResourceId == resource.Id && r.Quantity == 3));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_fails_when_resource_does_not_exist()
    {
        var act = () => _sut.CreateAsync(Request(Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "ResourceId");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_fails_when_resource_is_inactive()
    {
        var resource = GivenResource(active: false);

        var act = () => _sut.CreateAsync(Request(resource.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not active*");
    }

    [Fact]
    public async Task Create_fails_when_quantity_exceeds_capacity()
    {
        var resource = GivenResource(capacity: 2);

        var act = () => _sut.CreateAsync(Request(resource.Id, quantity: 3), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public async Task Approve_unknown_reservation_throws_not_found()
    {
        var act = () => _sut.ApproveAsync(Guid.NewGuid(), expectedVersion: null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Approve_already_rejected_reservation_throws_domain_exception_and_does_not_save()
    {
        var reservation = new Reservation(
            Guid.NewGuid(), "P0042", RequesterRole.Profesor,
            LocalStart.UtcDateTime, LocalStart.AddHours(1).UtcDateTime, 1, "Física", Now.UtcDateTime);
        reservation.Reject(null);
        _reservations.GetForUpdateAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(reservation);

        var act = () => _sut.ApproveAsync(reservation.Id, expectedVersion: null, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
