using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Rules;
using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Application.Rules.Evaluators;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using CitSync.Rules.Domain.RuleSets;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using static CitSync.Rules.UnitTests.Rules.RuleTestData;

namespace CitSync.Rules.UnitTests.Rules;

public class RuleEngineTests
{
    private readonly IResourceRepository _resources = Substitute.For<IResourceRepository>();
    private readonly IRuleSetRepository _ruleSets = Substitute.For<IRuleSetRepository>();
    private readonly IReservationRepository _reservations = Substitute.For<IReservationRepository>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();
    private readonly Resource _lab = Lab(capacity: 4);

    public RuleEngineTests()
    {
        _clock.GetUtcNow().Returns(new DateTimeOffset(NowUtc));
        _resources.GetByIdAsync(_lab.Id, Arg.Any<CancellationToken>()).Returns(_lab);
        _ruleSets.GetActiveVersionsForAsync(default, default, default).ReturnsForAnyArgs([]);
        _reservations.GetOccupiedSlotsAsync(default, default, default, default, default, default).ReturnsForAnyArgs([]);
    }

    private IRuleEvaluator[] AllEvaluators() =>
    [
        new OpeningHoursEvaluator(), new MaxDurationEvaluator(), new NoOverlapEvaluator(_reservations),
        new RoleRestrictionEvaluator(), new BlackoutDatesEvaluator(), new LeadTimeEvaluator(),
    ];

    private RuleEngine Engine(IEnumerable<IRuleEvaluator>? evaluators = null) =>
        new(evaluators ?? AllEvaluators(), _resources, _ruleSets, _clock, new EvaluateReservationRequestValidator());

    private EvaluateReservationRequest Request(int fromHour, int toHour, int quantity = 1) => new(
        _lab.Id, "A00000", RequesterRole.Alumno,
        new DateTimeOffset(Monday(fromHour), SchoolOffset), new DateTimeOffset(Monday(toHour), SchoolOffset),
        quantity, "Robótica");

    /// <summary>Builds a rule set whose version 1 is Active, with the given rules.</summary>
    private static ActiveRuleSetVersion Active(string name, RuleScope scope, params (RuleType Type, string Json)[] rules)
    {
        var ruleSet = new RuleSet(name, null, scope, scope == RuleScope.School ? null : "key", "autor.demo", NowUtc);
        var version = ruleSet.AddVersion(rules.Select(r => new RuleDefinition(r.Type, r.Json)), "autor.demo", NowUtc);
        version.Submit("autor.demo", NowUtc);
        ruleSet.ApproveVersion(version.Number, "revisor.demo", NowUtc);
        return new ActiveRuleSetVersion(ruleSet.Id, ruleSet.Name, ruleSet.Scope, version);
    }

    [Fact]
    public void Engine_fails_fast_when_a_rule_type_has_no_evaluator()
    {
        var act = () => Engine([new MaxDurationEvaluator()]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*OpeningHours*");
    }

    [Fact]
    public async Task Without_active_rule_sets_everything_is_allowed()
    {
        var result = await Engine().EvaluateAsync(Request(9, 11), CancellationToken.None);

        result.Allowed.Should().BeTrue();
        result.Violations.Should().BeEmpty();
        result.AppliedRuleSets.Should().BeEmpty();
    }

    [Fact]
    public async Task Most_specific_scope_wins_per_rule_type_and_other_types_are_inherited()
    {
        var school = Active("Escuela", RuleScope.School,
            (RuleType.OpeningHours, """{"schedule":[{"days":["Monday"],"open":"07:00","close":"20:00"}]}"""),
            (RuleType.MaxDuration, """{"maxMinutes":60}"""));
        var lab = Active("Laboratorio", RuleScope.Resource,
            (RuleType.OpeningHours, """{"schedule":[{"days":["Monday"],"open":"10:00","close":"14:00"}]}"""));
        _ruleSets.GetActiveVersionsForAsync(_lab.Id, _lab.Building, Arg.Any<CancellationToken>()).Returns([school, lab]);

        var result = await Engine().EvaluateAsync(Request(9, 11), CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.Violations.Should().HaveCount(2);
        // The lab's stricter hours replaced the school's (9:00 is before 10:00)...
        result.Violations.Should().ContainSingle(v => v.Rule == RuleType.OpeningHours)
            .Which.RuleSetName.Should().Be("Laboratorio");
        // ...and the school's MaxDuration still applies because the lab doesn't define one.
        result.Violations.Should().ContainSingle(v => v.Rule == RuleType.MaxDuration)
            .Which.RuleSetName.Should().Be("Escuela");
        result.AppliedRuleSets.Select(a => a.Name).Should().Equal("Laboratorio", "Escuela");
    }

    [Fact]
    public async Task NoOverlap_uses_the_peak_of_existing_reservations()
    {
        var rules = Active("Laboratorio", RuleScope.Resource, (RuleType.NoOverlap, "{}"));
        _ruleSets.GetActiveVersionsForAsync(_lab.Id, _lab.Building, Arg.Any<CancellationToken>()).Returns([rules]);
        var request = Request(9, 11, quantity: 2);
        _reservations.GetOccupiedSlotsAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs([new OccupiedSlot(request.Start.UtcDateTime, request.End.UtcDateTime, 3)]);

        var result = await Engine().EvaluateAsync(request, CancellationToken.None);

        result.Allowed.Should().BeFalse();
        result.Violations.Should().ContainSingle().Which.Code.Should().Be("CapacityExceeded");
    }

    [Fact]
    public async Task Unknown_resource_is_a_validation_error()
    {
        var request = Request(9, 10) with { ResourceId = Guid.NewGuid() };

        var act = () => Engine().EvaluateAsync(request, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "ResourceId");
    }

    [Fact]
    public void Validate_reports_bad_parameters_for_the_given_type()
    {
        Engine().Validate(RuleType.LeadTime, """{"hours":24}""").Should().NotBeEmpty();
        Engine().Validate(RuleType.LeadTime, """{"minHours":24}""").Should().BeEmpty();
    }
}
