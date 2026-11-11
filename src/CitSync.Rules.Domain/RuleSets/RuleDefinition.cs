namespace CitSync.Rules.Domain.RuleSets;

/// <summary>
/// One rule inside a version, e.g. MaxDuration with {"maxMinutes":120,"role":"Alumno"}.
/// Value object: it has no identity of its own and never changes after the version is created.
/// The parameters are stored as a JSON object; the rule engine (phase 4) parses them per type.
/// </summary>
public sealed class RuleDefinition
{
    public RuleType Type { get; private set; }

    /// <summary>JSON object with the rule's parameters.</summary>
    public string Parameters { get; private set; }

    public RuleDefinition(RuleType type, string parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters);
        Type = type;
        Parameters = parameters;
    }
}
