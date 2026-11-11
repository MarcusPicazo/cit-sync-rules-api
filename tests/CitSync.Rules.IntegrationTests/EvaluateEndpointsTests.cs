using System.Net;
using System.Net.Http.Json;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.Resources;
using CitSync.Rules.Application.Rules;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using CitSync.Rules.Domain.RuleSets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using static CitSync.Rules.IntegrationTests.RuleSetsEndpointsTests;

namespace CitSync.Rules.IntegrationTests;

/// <summary>
/// POST /rules/evaluate end to end. Every test uses its own resource / building so the shared
/// database never leaks rules between tests (and no test creates a School-wide rule set).
/// </summary>
[Collection(ApiTestGroup.Name)]
public class EvaluateEndpointsTests(RulesApiFactory factory)
{
    private static readonly Uri EvaluateUri = new("/api/v1/rules/evaluate", UriKind.Relative);
    private static readonly string[] OnlyAlumno = ["Alumno"];

    // Monday 2030-04-01, school time (UTC-6).
    private static DateTimeOffset Monday(int hour, int minute = 0) =>
        new(2030, 4, 1, hour, minute, 0, TimeSpan.FromHours(-6));

    private static async Task<ResourceResponse> CreateResourceAsync(HttpClient client, int capacity, string building)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/resources", UriKind.Relative),
            new CreateResourceRequest($"Lab {Guid.NewGuid():N}", ResourceCategory.Lab, capacity, building),
            TestJson.Options);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ResourceResponse>();
    }

    /// <summary>Creates a rule set for the scope and takes version 1 to Active (author ≠ reviewer).</summary>
    private async Task ActivateRulesAsync(RuleScope scope, string scopeKey, params object[] rules)
    {
        using var author = factory.CreateClient(Author);
        using var reviewer = factory.CreateClient(Reviewer);

        var created = await author.PostAsJsonAsync(
            new Uri("/api/v1/rulesets", UriKind.Relative),
            new CreateRuleSetRequest($"Reglas {Guid.NewGuid():N}", null, scope, scopeKey),
            TestJson.Options);
        created.EnsureSuccessStatusCode();
        var ruleSet = await created.ReadAsync<RuleSetResponse>();

        var version = await author.PostAsJsonAsync(
            new Uri($"/api/v1/rulesets/{ruleSet.Id}/versions", UriKind.Relative), new { rules }, TestJson.Options);
        version.EnsureSuccessStatusCode();

        (await PostVersionActionAsync(author, ruleSet.Id, 1, "submit")).EnsureSuccessStatusCode();
        (await PostVersionActionAsync(reviewer, ruleSet.Id, 1, "approve")).EnsureSuccessStatusCode();
    }

    private static async Task<EvaluationResponse> EvaluateAsync(
        HttpClient client, Guid resourceId, DateTimeOffset start, DateTimeOffset end, int quantity = 1,
        RequesterRole role = RequesterRole.Alumno, Guid? exclude = null)
    {
        var response = await client.PostAsJsonAsync(
            EvaluateUri,
            new EvaluateReservationRequest(resourceId, "A00000", role, start, end, quantity, "Física", exclude),
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<EvaluationResponse>();
    }

    [Fact]
    public async Task Resource_without_rules_is_allowed()
    {
        using var client = factory.CreateClient("alumno.demo");
        var resource = await CreateResourceAsync(client, 5, $"Edificio {Guid.NewGuid():N}");

        var result = await EvaluateAsync(client, resource.Id, Monday(9), Monday(10));

        result.Allowed.Should().BeTrue();
        result.AppliedRuleSets.Should().BeEmpty();
    }

    [Fact]
    public async Task NoOverlap_counts_real_reservations_by_peak_occupancy()
    {
        using var client = factory.CreateClient("alumno.demo");
        var resource = await CreateResourceAsync(client, 2, $"Edificio {Guid.NewGuid():N}");
        await ActivateRulesAsync(RuleScope.Resource, resource.Id.ToString(), new { type = "NoOverlap", parameters = new { } });

        // An existing Pending reservation takes both units from 9:00 to 10:00.
        var existing = await (await client.PostAsJsonAsync(
                new Uri("/api/v1/reservations", UriKind.Relative),
                new CreateReservationRequest(resource.Id, "P0001", RequesterRole.Profesor, Monday(9), Monday(10), 2, "Química"),
                TestJson.Options))
            .ReadAsync<ReservationResponse>();

        var overlapping = await EvaluateAsync(client, resource.Id, Monday(9, 30), Monday(10, 30));
        var backToBack = await EvaluateAsync(client, resource.Id, Monday(10), Monday(11));
        var reEvaluatingItself = await EvaluateAsync(client, resource.Id, Monday(9), Monday(10), 2, exclude: existing.Id);

        overlapping.Allowed.Should().BeFalse();
        overlapping.Violations.Should().ContainSingle().Which.Code.Should().Be("CapacityExceeded");
        backToBack.Allowed.Should().BeTrue("[9,10) and [10,11) don't overlap");
        reEvaluatingItself.Allowed.Should().BeTrue("a reservation never collides with itself");
    }

    [Fact]
    public async Task Building_rules_are_inherited_by_its_resources()
    {
        var building = $"Edificio {Guid.NewGuid():N}";
        using var client = factory.CreateClient("alumno.demo");
        var resource = await CreateResourceAsync(client, 5, building);
        await ActivateRulesAsync(RuleScope.Building, building,
            new { type = "RoleRestriction", parameters = new { deniedRoles = OnlyAlumno } });

        var alumno = await EvaluateAsync(client, resource.Id, Monday(9), Monday(10));
        var profesor = await EvaluateAsync(client, resource.Id, Monday(9), Monday(10), role: RequesterRole.Profesor);

        alumno.Allowed.Should().BeFalse();
        var violation = alumno.Violations.Should().ContainSingle().Subject;
        violation.Rule.Should().Be(RuleType.RoleRestriction);
        violation.RuleSetVersion.Should().Be(1);
        alumno.AppliedRuleSets.Should().ContainSingle().Which.Scope.Should().Be(RuleScope.Building);
        profesor.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Resource_rules_override_building_rules_of_the_same_type()
    {
        var building = $"Edificio {Guid.NewGuid():N}";
        using var client = factory.CreateClient("alumno.demo");
        var resource = await CreateResourceAsync(client, 5, building);
        await ActivateRulesAsync(RuleScope.Building, building,
            new { type = "MaxDuration", parameters = new { maxMinutes = 60 } });
        await ActivateRulesAsync(RuleScope.Resource, resource.Id.ToString(),
            new { type = "MaxDuration", parameters = new { maxMinutes = 180 } });

        var result = await EvaluateAsync(client, resource.Id, Monday(9), Monday(11));

        result.Allowed.Should().BeTrue("the resource allows 180 minutes, overriding the building's 60");
        result.AppliedRuleSets.Should().HaveCount(2);
    }

    [Fact]
    public async Task Malformed_request_returns_400()
    {
        using var client = factory.CreateClient("alumno.demo");

        var response = await client.PostAsJsonAsync(
            EvaluateUri,
            new EvaluateReservationRequest(Guid.Empty, "A00000", RequesterRole.Alumno, Monday(10), Monday(9), 0, null),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys
            .Should().Contain(["ResourceId", "End", "Quantity"]);
    }

    [Fact]
    public async Task Version_with_misspelled_rule_parameter_is_rejected_with_400()
    {
        using var author = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(author);

        var response = await author.PostAsJsonAsync(
            new Uri($"/api/v1/rulesets/{ruleSet.Id}/versions", UriKind.Relative),
            new { rules = new object[] { new { type = "MaxDuration", parameters = new { maxMinute = 90 } } } },
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Should().ContainKey("Rules[0].Parameters");
    }
}
