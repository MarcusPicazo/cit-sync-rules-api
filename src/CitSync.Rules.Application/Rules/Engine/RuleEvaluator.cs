using System.Text.Json;
using System.Text.Json.Serialization;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.Rules.Engine;

/// <summary>Everything a rule may need to decide, computed once per evaluation.</summary>
public sealed class RuleContext
{
    public required Resource Resource { get; init; }
    public required RequesterRole RequesterRole { get; init; }
    public required DateTime StartUtc { get; init; }
    public required DateTime EndUtc { get; init; }
    public required int Quantity { get; init; }
    public required DateTime NowUtc { get; init; }
    public Guid? ExcludeReservationId { get; init; }

    /// <summary>Start/End in the school's time zone: opening hours and holidays are local concepts.</summary>
    public DateTimeOffset LocalStart => SchoolTimeZone.ToSchoolTime(StartUtc);

    public DateTimeOffset LocalEnd => SchoolTimeZone.ToSchoolTime(EndUtc);

    public TimeSpan Duration => EndUtc - StartUtc;
}

/// <summary>One broken rule. The engine adds which rule set / version it came from.</summary>
public sealed record RuleViolation(string Code, string Message);

/// <summary>
/// STRATEGY: one implementation per RuleType. The engine picks the strategy by Type,
/// so adding a new rule type means adding a class, not editing a switch.
/// </summary>
public interface IRuleEvaluator
{
    RuleType Type { get; }

    /// <summary>Errors in the stored parameters (empty = valid). Used when a version is created.</summary>
    IReadOnlyList<string> ValidateParameters(string parametersJson);

    ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        string parametersJson, RuleContext context, CancellationToken cancellationToken);
}

/// <summary>
/// TEMPLATE METHOD: the base class owns the fixed steps (parse JSON → validate → evaluate);
/// each subclass only fills in its typed parameters and its decision.
/// </summary>
public abstract class RuleEvaluator<TParameters> : IRuleEvaluator
    where TParameters : class
{
    protected static readonly IReadOnlyList<RuleViolation> NoViolations = [];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        // A typo like "maxMinute" must be an error, not a silently ignored property.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public abstract RuleType Type { get; }

    public IReadOnlyList<string> ValidateParameters(string parametersJson)
    {
        try
        {
            var parameters = Parse(parametersJson);
            return Validate(parameters).ToList();
        }
        catch (JsonException ex)
        {
            return [$"Invalid parameters for {Type}: {ex.Message}"];
        }
    }

    public ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        string parametersJson, RuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return EvaluateAsync(Parse(parametersJson), context, cancellationToken);
    }

    /// <summary>Semantic checks on the parameters (e.g. "maxMinutes must be positive").</summary>
    protected abstract IEnumerable<string> Validate(TParameters parameters);

    protected abstract ValueTask<IReadOnlyList<RuleViolation>> EvaluateAsync(
        TParameters parameters, RuleContext context, CancellationToken cancellationToken);

    /// <summary>Most rules are synchronous: ValueTask lets them return without allocating a Task.</summary>
    protected static ValueTask<IReadOnlyList<RuleViolation>> Result(params RuleViolation[] violations) =>
        ValueTask.FromResult<IReadOnlyList<RuleViolation>>(violations.Length == 0 ? NoViolations : violations);

    private static TParameters Parse(string json) =>
        JsonSerializer.Deserialize<TParameters>(json, Json)
        ?? throw new JsonException("Parameters cannot be null.");
}
