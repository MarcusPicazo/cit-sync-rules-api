using CitSync.Rules.Domain.Common;

namespace CitSync.Rules.Domain.RuleSets;

/// <summary>
/// An immutable snapshot of rules that goes through an approval workflow.
/// Rules are never edited in place: to change them, create a new version.
/// </summary>
public sealed class RuleSetVersion
{
    public const int ActorMaxLength = 100;
    public const int CommentMaxLength = 500;

    private readonly List<RuleDefinition> _rules = [];

    public Guid Id { get; private set; }
    public Guid RuleSetId { get; private set; }
    public int Number { get; private set; }
    public RuleSetVersionState State { get; private set; }
    public IReadOnlyList<RuleDefinition> Rules => _rules;

    public string CreatedBy { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTime? SubmittedUtc { get; private set; }
    public string? ReviewedBy { get; private set; }
    public DateTime? ReviewedUtc { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTime? ActivatedUtc { get; private set; }
    public DateTime? RetiredUtc { get; private set; }

    /// <summary>SQL Server rowversion: changes on every UPDATE (optimistic concurrency).</summary>
    public byte[] RowVersion { get; private set; } = [];

    // Only RuleSet.AddVersion creates versions, so numbering stays consistent.
    internal RuleSetVersion(
        Guid ruleSetId, int number, IEnumerable<RuleDefinition> rules, string createdBy, DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        _rules.AddRange(rules);
        if (_rules.Count == 0)
        {
            throw new ArgumentException("A version needs at least one rule.", nameof(rules));
        }

        Id = Guid.NewGuid();
        RuleSetId = ruleSetId;
        Number = number;
        State = RuleSetVersionState.Draft;
        CreatedBy = createdBy;
        CreatedUtc = createdUtc;
    }

    private RuleSetVersion()
    {
        CreatedBy = null!;
    }

    public void Submit(string actor, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        EnsureState(RuleSetVersionState.Draft, "submit");

        State = RuleSetVersionState.PendingApproval;
        SubmittedBy = actor;
        SubmittedUtc = utcNow;
    }

    public void Reject(string actor, DateTime utcNow, string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        EnsureState(RuleSetVersionState.PendingApproval, "reject");

        State = RuleSetVersionState.Rejected;
        ReviewedBy = actor;
        ReviewedUtc = utcNow;
        ReviewComment = comment.Trim();
    }

    // Internal: approving must go through RuleSet.ApproveVersion so the previous Active version is retired.
    internal void Approve(string actor, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        EnsureState(RuleSetVersionState.PendingApproval, "approve");

        // Four-eyes principle: whoever submitted the version cannot approve it.
        // OrdinalIgnoreCase: identifiers are compared byte by byte (no culture rules), and "Ana" == "ana".
        if (string.Equals(actor, SubmittedBy, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Version {Number} cannot be approved by '{actor}': whoever submits a version cannot approve it (four-eyes principle).");
        }

        State = RuleSetVersionState.Active;
        ReviewedBy = actor;
        ReviewedUtc = utcNow;
        ActivatedUtc = utcNow;
    }

    internal void Retire(DateTime utcNow)
    {
        EnsureState(RuleSetVersionState.Active, "retire");
        State = RuleSetVersionState.Retired;
        RetiredUtc = utcNow;
    }

    private void EnsureState(RuleSetVersionState expected, string action)
    {
        if (State != expected)
        {
            throw new DomainException(
                $"Cannot {action} version {Number}: it is {State}, it must be {expected}.");
        }
    }
}
