using System.Text.Json;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.RuleSets;
using FluentValidation.TestHelper;

namespace CitSync.Rules.UnitTests.Application;

public class CreateRuleSetRequestValidatorTests
{
    private readonly CreateRuleSetRequestValidator _validator = new();

    [Theory]
    [InlineData(RuleScope.School, null)]
    [InlineData(RuleScope.Building, "Edificio A")]
    [InlineData(RuleScope.Resource, "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void Valid_scope_and_key_combinations_pass(RuleScope scope, string? scopeKey)
    {
        _validator.TestValidate(new CreateRuleSetRequest("Reglas", null, scope, scopeKey))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(RuleScope.School, "Edificio A")]
    [InlineData(RuleScope.Building, null)]
    [InlineData(RuleScope.Resource, "not-a-guid")]
    public void Invalid_scope_and_key_combinations_fail_on_ScopeKey(RuleScope scope, string? scopeKey)
    {
        _validator.TestValidate(new CreateRuleSetRequest("Reglas", null, scope, scopeKey))
            .ShouldHaveValidationErrorFor(r => r.ScopeKey);
    }
}

public class CreateRuleSetVersionRequestValidatorTests
{
    private readonly CreateRuleSetVersionRequestValidator _validator = new();

    [Fact]
    public void Rules_with_object_parameters_pass()
    {
        var request = new CreateRuleSetVersionRequest(
            [new RuleDefinitionDto(RuleType.MaxDuration, JsonSerializer.SerializeToElement(new { maxMinutes = 120 }))]);

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_rules_fail()
    {
        _validator.TestValidate(new CreateRuleSetVersionRequest([]))
            .ShouldHaveValidationErrorFor(r => r.Rules);
    }

    [Fact]
    public void Non_object_parameters_and_unknown_type_fail_per_item()
    {
        var request = new CreateRuleSetVersionRequest(
        [
            new RuleDefinitionDto(RuleType.LeadTime, JsonSerializer.SerializeToElement(new { hours = 24 })),
            new RuleDefinitionDto((RuleType)99, JsonSerializer.SerializeToElement(42)),
        ]);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Rules[1].Type");
        result.ShouldHaveValidationErrorFor("Rules[1].Parameters");
        result.ShouldNotHaveValidationErrorFor("Rules[0].Parameters");
    }
}

public class RejectRuleSetVersionRequestValidatorTests
{
    [Fact]
    public void Comment_is_required()
    {
        new RejectRuleSetVersionRequestValidator()
            .TestValidate(new RejectRuleSetVersionRequest(" "))
            .ShouldHaveValidationErrorFor(r => r.Comment);
    }
}
