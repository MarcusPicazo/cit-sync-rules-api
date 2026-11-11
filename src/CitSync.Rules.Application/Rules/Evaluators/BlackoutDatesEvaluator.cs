using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Rules.Evaluators;

/// <example>{"periods":[{"from":"2030-12-20","to":"2031-01-06","reason":"Vacaciones de invierno"}]}</example>
public sealed record BlackoutDatesParameters(IReadOnlyList<BlackoutPeriod> Periods);

/// <summary>Inclusive range of school-local dates. A single holiday has From == To.</summary>
public sealed record BlackoutPeriod(DateOnly From, DateOnly To, string? Reason = null);

public sealed class BlackoutDatesEvaluator : RuleEvaluator<BlackoutDatesParameters>
{
    public override RuleType Type => RuleType.BlackoutDates;

    protected override IEnumerable<string> Validate(BlackoutDatesParameters parameters)
    {
        if (parameters.Periods is not { Count: > 0 })
        {
            yield return "'periods' must have at least one period.";
            yield break;
        }

        foreach (var period in parameters.Periods.Where(p => p.From > p.To))
        {
            yield return $"Period {period.From:yyyy-MM-dd}..{period.To:yyyy-MM-dd}: 'from' must not be after 'to'.";
        }
    }

    protected override ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        BlackoutDatesParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        // TODO(Marcus): implement the BlackoutDates rule.
        //   - Work with SCHOOL-LOCAL dates: DateOnly.FromDateTime(context.LocalStart.DateTime) and the same
        //     for LocalEnd. (A 23:30 UTC start is still "yesterday" in Mexico City.)
        //   - The reservation covers the local days [startDay, endDay]; a period covers [From, To].
        //     Both are INCLUSIVE ranges. Two inclusive ranges overlap when
        //     a.start <= b.end AND b.start <= a.end  — the same idea as the NoOverlap SQL query.
        //   - Return one violation per overlapping period, code "BlackoutDate", and include the
        //     period's Reason in the message when it has one.
        // Then remove the Skip from the BlackoutDates_* tests in UnitTests/Rules/EvaluatorTests.cs.
        return Result();
    }
}
