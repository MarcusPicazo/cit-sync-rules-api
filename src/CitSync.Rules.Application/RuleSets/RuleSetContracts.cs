using System.Text.Json;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.RuleSets;

namespace CitSync.Rules.Application.RuleSets;

public sealed record CreateRuleSetRequest(string Name, string? Description, RuleScope Scope, string? ScopeKey);

/// <summary>Parameters is any JSON object; each rule type defines its shape (validated in phase 4).</summary>
public sealed record RuleDefinitionDto(RuleType Type, JsonElement Parameters);

public sealed record CreateRuleSetVersionRequest(IReadOnlyList<RuleDefinitionDto> Rules);

public sealed record RejectRuleSetVersionRequest(string Comment);

public sealed class ListRuleSetsQuery : PageQuery
{
    public RuleScope? Scope { get; init; }
}

public sealed record RuleSetSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    RuleScope Scope,
    string ScopeKey,
    int? ActiveVersionNumber,
    int LatestVersionNumber,
    string CreatedBy,
    DateTime CreatedUtc)
{
    public static RuleSetSummaryResponse From(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return new(
            ruleSet.Id,
            ruleSet.Name,
            ruleSet.Description,
            ruleSet.Scope,
            ruleSet.ScopeKey,
            ruleSet.ActiveVersionNumber,
            ruleSet.LatestVersionNumber,
            ruleSet.CreatedBy,
            ruleSet.CreatedUtc);
    }
}

public sealed record RuleSetResponse(
    Guid Id,
    string Name,
    string? Description,
    RuleScope Scope,
    string ScopeKey,
    int? ActiveVersionNumber,
    int LatestVersionNumber,
    string CreatedBy,
    DateTime CreatedUtc,
    IReadOnlyList<RuleSetVersionResponse> Versions,
    string Version)
{
    public static RuleSetResponse From(RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return new(
            ruleSet.Id,
            ruleSet.Name,
            ruleSet.Description,
            ruleSet.Scope,
            ruleSet.ScopeKey,
            ruleSet.ActiveVersionNumber,
            ruleSet.LatestVersionNumber,
            ruleSet.CreatedBy,
            ruleSet.CreatedUtc,
            ruleSet.Versions.OrderBy(v => v.Number).Select(RuleSetVersionResponse.From).ToList(),
            ConcurrencyGuard.ToToken(ruleSet.RowVersion));
    }
}

public sealed record RuleDefinitionResponse(RuleType Type, JsonElement Parameters);

public sealed record RuleSetVersionResponse(
    Guid Id,
    Guid RuleSetId,
    int Number,
    RuleSetVersionState State,
    IReadOnlyList<RuleDefinitionResponse> Rules,
    string CreatedBy,
    DateTime CreatedUtc,
    string? SubmittedBy,
    DateTime? SubmittedUtc,
    string? ReviewedBy,
    DateTime? ReviewedUtc,
    string? ReviewComment,
    DateTime? ActivatedUtc,
    DateTime? RetiredUtc,
    string Version)
{
    public static RuleSetVersionResponse From(RuleSetVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new(
            version.Id,
            version.RuleSetId,
            version.Number,
            version.State,
            version.Rules.Select(r => new RuleDefinitionResponse(r.Type, JsonText.ToElement(r.Parameters))).ToList(),
            version.CreatedBy,
            version.CreatedUtc,
            version.SubmittedBy,
            version.SubmittedUtc,
            version.ReviewedBy,
            version.ReviewedUtc,
            version.ReviewComment,
            version.ActivatedUtc,
            version.RetiredUtc,
            ConcurrencyGuard.ToToken(version.RowVersion));
    }
}
