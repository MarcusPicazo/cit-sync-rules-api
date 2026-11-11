using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Rules.Evaluators;

/// <example>{"schedule":[{"days":["Monday","Tuesday"],"open":"07:00","close":"20:00"}]}</example>
public sealed record OpeningHoursParameters(IReadOnlyList<OpeningWindow> Schedule);

public sealed record OpeningWindow(IReadOnlyList<DayOfWeek> Days, TimeOnly Open, TimeOnly Close);

/// <summary>The whole reservation must fit inside one opening window of its (local) day.</summary>
public sealed class OpeningHoursEvaluator : RuleEvaluator<OpeningHoursParameters>
{
    public override RuleType Type => RuleType.OpeningHours;

    protected override IEnumerable<string> Validate(OpeningHoursParameters parameters)
    {
        if (parameters.Schedule is not { Count: > 0 })
        {
            yield return "'schedule' must have at least one window.";
            yield break;
        }

        foreach (var window in parameters.Schedule)
        {
            if (window.Days is not { Count: > 0 })
            {
                yield return "Every window needs at least one day.";
            }

            if (window.Open >= window.Close)
            {
                yield return $"Window {window.Open:HH\\:mm}-{window.Close:HH\\:mm}: 'open' must be before 'close'.";
            }
        }
    }

    protected override ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        OpeningHoursParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        var start = context.LocalStart;
        var end = context.LocalEnd;

        if (start.Date != end.Date)
        {
            return Result(new RuleViolation(
                "SpansMultipleDays", "A reservation must start and end on the same day (school time)."));
        }

        var windows = parameters.Schedule.Where(w => w.Days.Contains(start.DayOfWeek)).ToList();
        if (windows.Count == 0)
        {
            return Result(new RuleViolation("ClosedDay", $"'{context.Resource.Name}' is closed on {start.DayOfWeek}."));
        }

        var from = TimeOnly.FromTimeSpan(start.TimeOfDay);
        var to = TimeOnly.FromTimeSpan(end.TimeOfDay);
        if (windows.Any(w => w.Open <= from && to <= w.Close))
        {
            return Result();
        }

        var hours = string.Join(", ", windows.Select(w => $"{w.Open:HH\\:mm}-{w.Close:HH\\:mm}"));
        return Result(new RuleViolation(
            "OutsideOpeningHours",
            $"{from:HH\\:mm}-{to:HH\\:mm} is outside the opening hours on {start.DayOfWeek} ({hours})."));
    }
}
