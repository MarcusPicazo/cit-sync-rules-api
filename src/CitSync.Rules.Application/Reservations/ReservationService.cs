using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Reservations;
using FluentValidation;
using FluentValidation.Results;

namespace CitSync.Rules.Application.Reservations;

public interface IReservationService
{
    Task<PagedResult<ReservationResponse>> ListAsync(ListReservationsQuery query, CancellationToken cancellationToken);

    Task<ReservationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ReservationResponse> CreateAsync(CreateReservationRequest request, CancellationToken cancellationToken);

    Task<ReservationResponse> ApproveAsync(Guid id, CancellationToken cancellationToken);

    Task<ReservationResponse> RejectAsync(Guid id, ChangeReservationStatusRequest request, CancellationToken cancellationToken);

    Task<ReservationResponse> CancelAsync(Guid id, ChangeReservationStatusRequest request, CancellationToken cancellationToken);
}

public sealed class ReservationService(
    IReservationRepository reservations,
    IResourceRepository resources,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<ListReservationsQuery> listValidator,
    IValidator<CreateReservationRequest> createValidator,
    IValidator<ChangeReservationStatusRequest> statusValidator) : IReservationService
{
    public async Task<PagedResult<ReservationResponse>> ListAsync(
        ListReservationsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var page = await reservations.ListAsync(query, cancellationToken);
        return page.Map(ReservationResponse.From);
    }

    public async Task<ReservationResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(Reservation), id);
        return ReservationResponse.From(reservation);
    }

    public async Task<ReservationResponse> CreateAsync(
        CreateReservationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Checks that need the database. They describe a bad request body, so 400 (not 404).
        var resource = await resources.GetByIdAsync(request.ResourceId, cancellationToken)
            ?? throw Invalid(nameof(request.ResourceId), $"Resource '{request.ResourceId}' does not exist.");

        if (!resource.IsActive)
        {
            throw Invalid(nameof(request.ResourceId), $"Resource '{resource.Name}' is not active.");
        }

        if (request.Quantity > resource.Capacity)
        {
            throw Invalid(
                nameof(request.Quantity),
                $"'{resource.Name}' has capacity {resource.Capacity}; requested {request.Quantity}.");
        }

        var reservation = new Reservation(
            request.ResourceId,
            request.RequesterId,
            request.RequesterRole,
            request.Start.UtcDateTime,
            request.End.UtcDateTime,
            request.Quantity,
            request.Subject,
            timeProvider.GetUtcNow().UtcDateTime);

        reservations.Add(reservation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReservationResponse.From(reservation);
    }

    public Task<ReservationResponse> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, r => r.Approve(), cancellationToken);

    public async Task<ReservationResponse> RejectAsync(
        Guid id, ChangeReservationStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await ChangeStatusAsync(id, r => r.Reject(request.Reason), cancellationToken);
    }

    public async Task<ReservationResponse> CancelAsync(
        Guid id, ChangeReservationStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await ChangeStatusAsync(id, r => r.Cancel(request.Reason), cancellationToken);
    }

    private async Task<ReservationResponse> ChangeStatusAsync(
        Guid id, Action<Reservation> transition, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(Reservation), id);

        transition(reservation); // the entity enforces the state machine (DomainException → 409)
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ReservationResponse.From(reservation);
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
