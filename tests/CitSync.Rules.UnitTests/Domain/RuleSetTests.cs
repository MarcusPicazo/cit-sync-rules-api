using CitSync.Rules.Domain.Common;
using CitSync.Rules.Domain.RuleSets;
using FluentAssertions;

namespace CitSync.Rules.UnitTests.Domain;

public class RuleSetTests
{
    private static readonly DateTime Now = new(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private const string Author = "coordinacion.demo";
    private const string Reviewer = "direccion.demo";

    private static RuleSet NewRuleSet() =>
        new("Reglas generales", "Demo", RuleScope.School, scopeKey: null, Author, Now);

    private static RuleDefinition[] SomeRules() =>
        [new(RuleType.MaxDuration, """{"maxMinutes":120}"""), new(RuleType.LeadTime, """{"hours":24}""")];

    private static RuleSetVersion PendingVersion(RuleSet ruleSet)
    {
        var version = ruleSet.AddVersion(SomeRules(), Author, Now);
        version.Submit(Author, Now);
        return version;
    }

    [Fact]
    public void School_scope_uses_a_fixed_scope_key()
    {
        var ruleSet = new RuleSet("Escuela", null, RuleScope.School, "ignored", Author, Now);

        ruleSet.ScopeKey.Should().Be(RuleSet.SchoolScopeKey);
    }

    [Fact]
    public void Building_scope_requires_a_scope_key()
    {
        var act = () => new RuleSet("Edificio A", null, RuleScope.Building, "  ", Author, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("scopeKey");
    }

    [Fact]
    public void AddVersion_numbers_versions_consecutively_as_drafts()
    {
        var ruleSet = NewRuleSet();

        var v1 = ruleSet.AddVersion(SomeRules(), Author, Now);
        var v2 = ruleSet.AddVersion(SomeRules(), Author, Now);

        v1.Number.Should().Be(1);
        v2.Number.Should().Be(2);
        v2.State.Should().Be(RuleSetVersionState.Draft);
        v2.Rules.Should().HaveCount(2);
        ruleSet.LatestVersionNumber.Should().Be(2);
        ruleSet.ActiveVersionNumber.Should().BeNull();
    }

    [Fact]
    public void AddVersion_without_rules_throws()
    {
        var act = () => NewRuleSet().AddVersion([], Author, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_moves_draft_to_pending_and_records_who()
    {
        var ruleSet = NewRuleSet();

        var version = PendingVersion(ruleSet);

        version.State.Should().Be(RuleSetVersionState.PendingApproval);
        version.SubmittedBy.Should().Be(Author);
        version.SubmittedUtc.Should().Be(Now);
    }

    [Fact]
    public void Submit_twice_throws_domain_exception()
    {
        var version = PendingVersion(NewRuleSet());

        var act = () => version.Submit(Author, Now);

        act.Should().Throw<DomainException>().WithMessage("*PendingApproval*");
    }

    [Fact]
    public void Approve_activates_the_version()
    {
        var ruleSet = NewRuleSet();
        var version = PendingVersion(ruleSet);

        ruleSet.ApproveVersion(version.Number, Reviewer, Now);

        version.State.Should().Be(RuleSetVersionState.Active);
        version.ReviewedBy.Should().Be(Reviewer);
        version.ActivatedUtc.Should().Be(Now);
        ruleSet.ActiveVersionNumber.Should().Be(1);
    }

    [Fact]
    public void Approving_a_new_version_retires_the_previous_active_one()
    {
        var ruleSet = NewRuleSet();
        var v1 = PendingVersion(ruleSet);
        ruleSet.ApproveVersion(1, Reviewer, Now);
        var v2 = PendingVersion(ruleSet);

        ruleSet.ApproveVersion(2, Reviewer, Now.AddDays(1));

        v1.State.Should().Be(RuleSetVersionState.Retired);
        v1.RetiredUtc.Should().Be(Now.AddDays(1));
        v2.State.Should().Be(RuleSetVersionState.Active);
        ruleSet.ActiveVersionNumber.Should().Be(2);
        ruleSet.Versions.Should().ContainSingle(v => v.State == RuleSetVersionState.Active);
    }

    [Fact]
    public void Approving_a_draft_throws_and_leaves_the_active_version_untouched()
    {
        var ruleSet = NewRuleSet();
        var v1 = PendingVersion(ruleSet);
        ruleSet.ApproveVersion(1, Reviewer, Now);
        ruleSet.AddVersion(SomeRules(), Author, Now); // v2 stays Draft

        var act = () => ruleSet.ApproveVersion(2, Reviewer, Now);

        act.Should().Throw<DomainException>().WithMessage("*Draft*");
        v1.State.Should().Be(RuleSetVersionState.Active);
        ruleSet.ActiveVersionNumber.Should().Be(1);
    }

    [Fact]
    public void Approving_an_unknown_version_throws()
    {
        var act = () => NewRuleSet().ApproveVersion(7, Reviewer, Now);

        act.Should().Throw<DomainException>().WithMessage("*no version 7*");
    }

    [Fact]
    public void Reject_requires_pending_and_stores_comment()
    {
        var ruleSet = NewRuleSet();
        var version = PendingVersion(ruleSet);

        version.Reject(Reviewer, Now, "  Falta la regla de días festivos ");

        version.State.Should().Be(RuleSetVersionState.Rejected);
        version.ReviewComment.Should().Be("Falta la regla de días festivos");
        var approveRejected = () => ruleSet.ApproveVersion(version.Number, Reviewer, Now);
        approveRejected.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_by_the_submitter_is_rejected()
    {
        var ruleSet = NewRuleSet();
        var version = PendingVersion(ruleSet); // submitted by Author

        var act = () => ruleSet.ApproveVersion(version.Number, Author.ToUpperInvariant(), Now);

        act.Should().Throw<DomainException>();
        version.State.Should().Be(RuleSetVersionState.PendingApproval);
    }
}
