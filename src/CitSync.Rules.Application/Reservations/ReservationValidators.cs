using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Reservations;
using FluentValidation;

namespace CitSync.Rules.Application.Reservations;

public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(r => r.ResourceId).NotEmpty();
        RuleFor(r => r.RequesterId).NotEmpty().MaximumLength(Reservation.RequesterIdMaxLength);
        RuleFor(r => r.RequesterRole).IsInEnum();
        RuleFor(r => r.Start).NotEmpty();
        RuleFor(r => r.End)
            .Cascade(CascadeMode.Stop) // if End is missing, don't also report "must be after Start"
            .NotEmpty()
            .GreaterThan(r => r.Start).WithMessage("'End' must be after 'Start'.");
        RuleFor(r => r.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(r => r.Subject).NotEmpty().MaximumLength(Reservation.SubjectMaxLength);

        // Opening hours, max duration, lead time, overlap... are NOT here on purpose:
        // they are configurable rules evaluated by the rule engine in phase 4.
    }
}

public sealed class ChangeReservationStatusRequestValidator : AbstractValidator<ChangeReservationStatusRequest>
{
    public ChangeReservationStatusRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(Reservation.StatusReasonMaxLength);
    }
}

public sealed class ListReservationsQueryValidator : AbstractValidator<ListReservationsQuery>
{
    public ListReservationsQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(q => q.Status).IsInEnum();

        // TODO(Marcus): when BOTH From and To are present, To must be after From.
        // Hint: RuleFor(...).GreaterThan(...).When(...). Both are nullable, so check how
        // FluentValidation compares nullable properties, and give it a clear message.
        // Then remove the Skip from To_must_be_after_From in
        // UnitTests/Application/ListReservationsQueryValidatorTests.cs.
    }
}
