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

        // Only when BOTH dates are present: an open-ended range (just From or just To) is valid.
        // From == To is rejected too, because it is an empty window.
        RuleFor(q => q.To)
            .GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.")
            .When(q => q.From.HasValue && q.To.HasValue);
    }
}
