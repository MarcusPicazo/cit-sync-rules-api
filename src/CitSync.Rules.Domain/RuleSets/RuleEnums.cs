namespace CitSync.Rules.Domain.RuleSets;

/// <summary>Where a rule set applies. There is at most one rule set per (scope, scope key).</summary>
public enum RuleScope
{
    School = 1,
    Building = 2,
    Resource = 3,
}

/// <summary>The rule types the evaluation engine (phase 4) understands.</summary>
public enum RuleType
{
    OpeningHours = 1,
    MaxDuration = 2,
    NoOverlap = 3,
    RoleRestriction = 4,
    BlackoutDates = 5,
    LeadTime = 6,
}

/// <summary>
/// Draft → PendingApproval → Active → Retired.
/// PendingApproval → Rejected (terminal: a fix means creating a new version).
/// </summary>
public enum RuleSetVersionState
{
    Draft = 1,
    PendingApproval = 2,
    Active = 3,
    Retired = 4,
    Rejected = 5,
}
