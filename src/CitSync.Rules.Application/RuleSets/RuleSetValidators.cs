using System.Text.Json;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.RuleSets;
using FluentValidation;
// Alias: inside AbstractValidator<T>, "RuleSet" resolves to FluentValidation's RuleSet(...) method.
using RuleSetEntity = CitSync.Rules.Domain.RuleSets.RuleSet;

namespace CitSync.Rules.Application.RuleSets;

public sealed class CreateRuleSetRequestValidator : AbstractValidator<CreateRuleSetRequest>
{
    public CreateRuleSetRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(RuleSetEntity.NameMaxLength);
        RuleFor(r => r.Description).MaximumLength(RuleSetEntity.DescriptionMaxLength);
        RuleFor(r => r.Scope).IsInEnum();

        // The scope key depends on the scope: three small rules instead of one big one.
        RuleFor(r => r.ScopeKey)
            .Empty().WithMessage("'Scope Key' must be empty for the School scope.")
            .When(r => r.Scope == RuleScope.School);

        RuleFor(r => r.ScopeKey)
            .NotEmpty().MaximumLength(RuleSetEntity.ScopeKeyMaxLength)
            .When(r => r.Scope == RuleScope.Building);

        RuleFor(r => r.ScopeKey)
            .NotEmpty()
            .Must(key => Guid.TryParse(key, out _)).WithMessage("'Scope Key' must be the resource id (a GUID).")
            .When(r => r.Scope == RuleScope.Resource);
    }
}

public sealed class CreateRuleSetVersionRequestValidator : AbstractValidator<CreateRuleSetVersionRequest>
{
    public const int MaxRules = 50;

    public CreateRuleSetVersionRequestValidator()
    {
        RuleFor(r => r.Rules)
            .NotEmpty().WithMessage("A version needs at least one rule.")
            .Must(rules => rules.Count <= MaxRules).WithMessage($"A version can have at most {MaxRules} rules.");

        // Validates every item of the collection; errors come back as "Rules[2].Parameters".
        RuleForEach(r => r.Rules).ChildRules(rule =>
        {
            rule.RuleFor(x => x.Type).IsInEnum();
            rule.RuleFor(x => x.Parameters)
                .Must(p => p.ValueKind == JsonValueKind.Object)
                .WithMessage("'Parameters' must be a JSON object.");
        });
    }
}

public sealed class RejectRuleSetVersionRequestValidator : AbstractValidator<RejectRuleSetVersionRequest>
{
    public RejectRuleSetVersionRequestValidator()
    {
        // A rejection without a reason is useless to whoever has to fix the rules.
        RuleFor(r => r.Comment).NotEmpty().MaximumLength(RuleSetVersion.CommentMaxLength);
    }
}

public sealed class ListRuleSetsQueryValidator : AbstractValidator<ListRuleSetsQuery>
{
    public ListRuleSetsQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(q => q.Scope).IsInEnum();
    }
}
