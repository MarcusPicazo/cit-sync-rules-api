using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Rules.Evaluators;

/// <example>{"countPending":true}  (false = only Approved reservations occupy capacity)</example>
public sealed record NoOverlapParameters(bool CountPending = true);

/// <summary>
/// The resource can't be over-booked: at every instant of the requested window,
/// (units already in use) + (units requested) must be ≤ resource capacity.
/// The only rule that needs the database: ONE sargable query (see docs/query-tuning.md).
/// </summary>
public sealed class NoOverlapEvaluator(IReservationRepository reservations) : RuleEvaluator<NoOverlapParameters>
{
    public override RuleType Type => RuleType.NoOverlap;

    protected override IEnumerable<string> Validate(NoOverlapParameters parameters) => [];

    protected override async ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        NoOverlapParameters parameters, RuleContext context, CancellationToken cancellationToken)
    {
        var occupied = await reservations.GetOccupiedSlotsAsync(
            context.Resource.Id,
            context.StartUtc,
            context.EndUtc,
            parameters.CountPending,
            context.ExcludeReservationId,
            cancellationToken);

        var peak = CapacityMath.PeakConcurrentQuantity(occupied, context.StartUtc, context.EndUtc);
        var capacity = context.Resource.Capacity;

        if (peak + context.Quantity <= capacity)
        {
            return NoViolations;
        }

        var available = Math.Max(0, capacity - peak);
        return
        [
            new RuleViolation(
                "CapacityExceeded",
                $"'{context.Resource.Name}' has {available} of {capacity} units free in that time; {context.Quantity} requested."),
        ];
    }
}
