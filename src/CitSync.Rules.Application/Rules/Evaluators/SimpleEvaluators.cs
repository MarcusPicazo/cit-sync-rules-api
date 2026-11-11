using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Rules.Evaluators;

/// <example>{"maxMinutes":120,"roles":["Alumno"]}  (roles omitted = applies to everyone)</example>
public sealed record MaxDurationParameters(int MaxMinutes, IReadOnlyList<RequesterRole>? Roles = null);

public sealed class MaxDurationEvaluator : RuleEvaluator<MaxDurationParameters>
{
    public override RuleType Type => RuleType.MaxDuration;

    protected override IEnumerable<string> Validate(MaxDurationParameters parameters)
    {
        if (parameters.MaxMinutes <= 0)
        {
            yield return "'maxMinutes' must be greater than 0.";
        }
    }

    protected override ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        MaxDurationParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        if (parameters.Roles is { Count: > 0 } roles && !roles.Contains(context.RequesterRole))
        {
            return Result(); // this limit is for other roles
        }

        var minutes = (int)Math.Ceiling(context.Duration.TotalMinutes);
        return minutes <= parameters.MaxMinutes
            ? Result()
            : Result(new RuleViolation(
                "MaxDurationExceeded",
                $"{minutes} minutes requested; the maximum for {context.RequesterRole} is {parameters.MaxMinutes}."));
    }
}

/// <example>{"deniedRoles":["Alumno"],"categories":["Equipment"]}  (categories omitted = any category)</example>
public sealed record RoleRestrictionParameters(
    IReadOnlyList<RequesterRole> DeniedRoles, IReadOnlyList<ResourceCategory>? Categories = null);

public sealed class RoleRestrictionEvaluator : RuleEvaluator<RoleRestrictionParameters>
{
    public override RuleType Type => RuleType.RoleRestriction;

    protected override IEnumerable<string> Validate(RoleRestrictionParameters parameters)
    {
        if (parameters.DeniedRoles is not { Count: > 0 })
        {
            yield return "'deniedRoles' must have at least one role.";
        }
    }

    protected override ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        RoleRestrictionParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        if (parameters.Categories is { Count: > 0 } categories && !categories.Contains(context.Resource.Category))
        {
            return Result();
        }

        return parameters.DeniedRoles.Contains(context.RequesterRole)
            ? Result(new RuleViolation(
                "RoleNotAllowed", $"{context.RequesterRole} cannot reserve '{context.Resource.Name}'."))
            : Result();
    }
}

/// <example>{"minHours":24}</example>
public sealed record LeadTimeParameters(int MinHours);

/// <summary>Bookings must be made at least N hours before they start.</summary>
public sealed class LeadTimeEvaluator : RuleEvaluator<LeadTimeParameters>
{
    public override RuleType Type => RuleType.LeadTime;

    protected override IEnumerable<string> Validate(LeadTimeParameters parameters)
    {
        if (parameters.MinHours < 0)
        {
            yield return "'minHours' cannot be negative.";
        }
    }

    protected override ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        LeadTimeParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        // TODO(Marcus): implement the LeadTime rule.
        //   - "Notice" = how long before the start the request is made: context.StartUtc - context.NowUtc.
        //     Subtracting two DateTime gives a TimeSpan.
        //   - If the notice is smaller than parameters.MinHours hours, return ONE violation with
        //     code "LeadTimeNotMet" and a message that says how many hours are required
        //     (look at TimeSpan.FromHours and the comparison operators on TimeSpan).
        //   - A start in the past is also "LeadTimeNotMet" (its notice is negative), no special case needed.
        //   - Otherwise return Result() (no violations).
        // Then remove the Skip from the LeadTime_* tests in UnitTests/Rules/EvaluatorTests.cs.
        return Result();
    }
}
