using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Domain.RuleSets;
using FluentValidation;
using FluentValidation.Results;

namespace CitSync.Rules.Application.Rules.Engine;

public interface IRuleEngine
{
    Task<EvaluationResponse> EvaluateAsync(EvaluateReservationRequest request, CancellationToken cancellationToken);
}

/// <summary>Validates rule parameters when a version is created (implemented by the engine).</summary>
public interface IRuleParametersValidator
{
    IReadOnlyList<string> Validate(RuleType type, string parametersJson);
}

/// <summary>
/// Orchestrates the evaluation: finds the Active rule sets that apply to the resource, resolves
/// which rule of each type wins (most specific scope), and delegates each rule to its strategy.
/// It knows NOTHING about what each rule means.
/// </summary>
public sealed class RuleEngine : IRuleEngine, IRuleParametersValidator
{
    private readonly Dictionary<RuleType, IRuleEvaluator> _evaluators;
    private readonly IResourceRepository _resources;
    private readonly IRuleSetRepository _ruleSets;
    private readonly TimeProvider _timeProvider;
    private readonly IValidator<EvaluateReservationRequest> _validator;

    public RuleEngine(
        IEnumerable<IRuleEvaluator> evaluators,
        IResourceRepository resources,
        IRuleSetRepository ruleSets,
        TimeProvider timeProvider,
        IValidator<EvaluateReservationRequest> validator)
    {
        // DI gives us every registered strategy; index them by the rule type they handle.
        // ToDictionary throws on duplicates, and the check below on gaps: misconfiguration fails fast.
        _evaluators = evaluators.ToDictionary(e => e.Type);
        var missing = Enum.GetValues<RuleType>().Where(t => !_evaluators.ContainsKey(t)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"No IRuleEvaluator registered for: {string.Join(", ", missing)}.");
        }

        _resources = resources;
        _ruleSets = ruleSets;
        _timeProvider = timeProvider;
        _validator = validator;
    }

    public IReadOnlyList<string> Validate(RuleType type, string parametersJson) =>
        _evaluators.TryGetValue(type, out var evaluator)
            ? evaluator.ValidateParameters(parametersJson)
            : [$"Unknown rule type '{type}'."];

    public async Task<EvaluationResponse> EvaluateAsync(
        EvaluateReservationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Same hard checks as POST /reservations: if creating would be a 400, evaluating is a 400 too.
        var resource = await _resources.GetByIdAsync(request.ResourceId, cancellationToken)
            ?? throw Invalid(nameof(request.ResourceId), $"Resource '{request.ResourceId}' does not exist.");
        if (!resource.IsActive)
        {
            throw Invalid(nameof(request.ResourceId), $"Resource '{resource.Name}' is not active.");
        }

        if (request.Quantity > resource.Capacity)
        {
            throw Invalid(
                nameof(request.Quantity), $"'{resource.Name}' has capacity {resource.Capacity}; requested {request.Quantity}.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var context = new RuleContext
        {
            Resource = resource,
            RequesterRole = request.RequesterRole,
            StartUtc = request.Start.UtcDateTime,
            EndUtc = request.End.UtcDateTime,
            Quantity = request.Quantity,
            NowUtc = now,
            ExcludeReservationId = request.ExcludeReservationId,
        };

        var active = (await _ruleSets.GetActiveVersionsForAsync(resource.Id, resource.Building, cancellationToken))
            .OrderByDescending(a => Specificity(a.Scope))
            .ToList();

        // Most specific scope wins PER RULE TYPE: a lab's own OpeningHours replaces the school's,
        // while rule types the lab doesn't define are inherited from the building / school.
        var effective = new Dictionary<RuleType, (ActiveRuleSetVersion Source, List<RuleDefinition> Rules)>();
        foreach (var source in active)
        {
            foreach (var group in source.Version.Rules.GroupBy(r => r.Type))
            {
                effective.TryAdd(group.Key, (source, group.ToList()));
            }
        }

        var violations = new List<RuleViolationResponse>();
        foreach (var (type, (source, rules)) in effective.OrderBy(e => e.Key))
        {
            foreach (var rule in rules)
            {
                var found = await _evaluators[type].EvaluateAsync(rule.Parameters, context, cancellationToken);
                violations.AddRange(found.Select(v => new RuleViolationResponse(
                    type, v.Code, v.Message, source.RuleSetId, source.Name, source.Version.Number)));
            }
        }

        return new EvaluationResponse(
            Allowed: violations.Count == 0,
            violations,
            active.Select(a => new AppliedRuleSetResponse(a.RuleSetId, a.Name, a.Scope, a.Version.Number)).ToList(),
            now);
    }

    private static int Specificity(RuleScope scope) => scope switch
    {
        RuleScope.Resource => 3,
        RuleScope.Building => 2,
        RuleScope.School => 1,
        _ => 0,
    };

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
