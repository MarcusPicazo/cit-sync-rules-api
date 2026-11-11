using CitSync.Rules.Domain.Common;

namespace CitSync.Rules.Domain.RuleSets;

/// <summary>
/// Aggregate root: the rules that apply to one scope (the whole school, a building or a resource).
/// All changes to its versions that affect other versions go through here, so the invariant
/// "at most one Active version" is enforced in ONE place.
/// </summary>
public sealed class RuleSet
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int ScopeKeyMaxLength = 80;
    public const string SchoolScopeKey = "school";

    private readonly List<RuleSetVersion> _versions = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public RuleScope Scope { get; private set; }

    /// <summary>"school" for School, the building name, or the resource id.</summary>
    public string ScopeKey { get; private set; }

    public int? ActiveVersionNumber { get; private set; }
    public int LatestVersionNumber { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    /// <summary>
    /// Concurrency token of the aggregate. AddVersion and ApproveVersion always modify this row,
    /// so two concurrent activations (or two "version 3"s) can't both win: the second gets 409.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyList<RuleSetVersion> Versions => _versions;

    public RuleSet(
        string name, string? description, RuleScope scope, string? scopeKey, string createdBy, DateTime createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Scope = scope;
        ScopeKey = NormalizeScopeKey(scope, scopeKey);
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
    }

    private RuleSet()
    {
        Name = null!;
        ScopeKey = null!;
        CreatedBy = null!;
    }

    public static string NormalizeScopeKey(RuleScope scope, string? scopeKey) => scope switch
    {
        RuleScope.School => SchoolScopeKey,
        RuleScope.Building or RuleScope.Resource when !string.IsNullOrWhiteSpace(scopeKey) => scopeKey.Trim(),
        RuleScope.Building or RuleScope.Resource =>
            throw new ArgumentException("A scope key is required for Building and Resource scopes.", nameof(scopeKey)),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown scope."),
    };

    public RuleSetVersion? FindVersion(int number) => _versions.FirstOrDefault(v => v.Number == number);

    public RuleSetVersion AddVersion(IEnumerable<RuleDefinition> rules, string actor, DateTime utcNow)
    {
        var version = new RuleSetVersion(Id, LatestVersionNumber + 1, rules, actor, utcNow);
        _versions.Add(version);
        LatestVersionNumber = version.Number;
        return version;
    }

    /// <summary>Activates a pending version and retires the one that was active, atomically.</summary>
    public RuleSetVersion ApproveVersion(int number, string actor, DateTime utcNow)
    {
        var candidate = FindVersion(number)
            ?? throw new DomainException($"Rule set '{Name}' has no version {number}.");
        var previous = _versions.FirstOrDefault(v => v.State == RuleSetVersionState.Active);

        candidate.Approve(actor, utcNow); // validates first: if it throws, nothing else changed
        previous?.Retire(utcNow);
        ActiveVersionNumber = candidate.Number;
        return candidate;
    }
}
