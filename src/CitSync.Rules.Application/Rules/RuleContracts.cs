using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.RuleSets;
using FluentValidation;

namespace CitSync.Rules.Application.Rules;

/// <summary>
/// A reservation someone WANTS to make. Same shape as CreateReservationRequest, plus an optional
/// id to ignore (when re-evaluating an existing reservation it must not collide with itself).
/// </summary>
public sealed record EvaluateReservationRequest(
    Guid ResourceId,
    string RequesterId,
    RequesterRole RequesterRole,
    DateTimeOffset Start,
    DateTimeOffset End,
    int Quantity,
    string? Subject,
    Guid? ExcludeReservationId = null);

public sealed record RuleViolationResponse(
    RuleType Rule,
    string Code,
    string Message,
    Guid RuleSetId,
    string RuleSetName,
    int RuleSetVersion);

public sealed record AppliedRuleSetResponse(Guid RuleSetId, string Name, RuleScope Scope, int Version);

public sealed record EvaluationResponse(
    bool Allowed,
    IReadOnlyList<RuleViolationResponse> Violations,
    IReadOnlyList<AppliedRuleSetResponse> AppliedRuleSets,
    DateTime EvaluatedAtUtc);

/// <summary>A reservation that already occupies part of the resource (input for NoOverlap).</summary>
public sealed record OccupiedSlot(DateTime StartUtc, DateTime EndUtc, int Quantity);

/// <summary>An Active version together with the rule set it belongs to.</summary>
public sealed record ActiveRuleSetVersion(Guid RuleSetId, string Name, RuleScope Scope, RuleSetVersion Version);

public sealed class EvaluateReservationRequestValidator : AbstractValidator<EvaluateReservationRequest>
{
    public EvaluateReservationRequestValidator()
    {
        RuleFor(r => r.ResourceId).NotEmpty();
        RuleFor(r => r.RequesterId).MaximumLength(Reservation.RequesterIdMaxLength);
        RuleFor(r => r.RequesterRole).IsInEnum();
        RuleFor(r => r.Start).NotEmpty();
        RuleFor(r => r.End)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .GreaterThan(r => r.Start).WithMessage("'End' must be after 'Start'.");
        RuleFor(r => r.Quantity).GreaterThanOrEqualTo(1);
    }
}
