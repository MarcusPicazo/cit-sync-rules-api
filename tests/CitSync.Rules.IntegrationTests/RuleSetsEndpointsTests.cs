using System.Net;
using System.Net.Http.Json;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.RuleSets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class RuleSetsEndpointsTests(RulesApiFactory factory)
{
    internal const string Author = "coordinacion.demo";
    internal const string Reviewer = "direccion.demo";

    private static readonly Uri RuleSetsUri = new("/api/v1/rulesets", UriKind.Relative);

    internal static object MaxDurationRules(int maxMinutes = 120) => new
    {
        rules = new object[]
        {
            new { type = "MaxDuration", parameters = new { maxMinutes, role = "Alumno" } },
            new { type = "LeadTime", parameters = new { hours = 24 } },
        },
    };

    internal static async Task<RuleSetResponse> CreateBuildingRuleSetAsync(HttpClient client)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync(
            RuleSetsUri,
            new CreateRuleSetRequest($"Reglas {id}", "Demo", RuleScope.Building, $"Edificio {id}"),
            TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<RuleSetResponse>();
    }

    internal static async Task<RuleSetVersionResponse> AddVersionAsync(HttpClient client, Guid ruleSetId)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/rulesets/{ruleSetId}/versions", UriKind.Relative), MaxDurationRules(), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<RuleSetVersionResponse>();
    }

    internal static Task<HttpResponseMessage> PostVersionActionAsync(
        HttpClient client, Guid ruleSetId, int number, string action, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/v1/rulesets/{ruleSetId}/versions/{number}/{action}", UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body, options: TestJson.Options),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return client.SendAsync(request);
    }

    private static async Task<RuleSetVersionResponse> ApprovedVersionAsync(HttpClient author, HttpClient reviewer, Guid ruleSetId)
    {
        var version = await AddVersionAsync(author, ruleSetId);
        (await PostVersionActionAsync(author, ruleSetId, version.Number, "submit")).EnsureSuccessStatusCode();
        var approve = await PostVersionActionAsync(reviewer, ruleSetId, version.Number, "approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        return await approve.ReadAsync<RuleSetVersionResponse>();
    }

    [Fact]
    public async Task Post_creates_rule_set_recording_the_actor()
    {
        using var client = factory.CreateClient(Author);

        var created = await CreateBuildingRuleSetAsync(client);

        created.CreatedBy.Should().Be(Author);
        created.Scope.Should().Be(RuleScope.Building);
        created.ActiveVersionNumber.Should().BeNull();
        created.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Second_rule_set_for_the_same_scope_returns_409()
    {
        using var client = factory.CreateClient(Author);
        var existing = await CreateBuildingRuleSetAsync(client);

        var response = await client.PostAsJsonAsync(
            RuleSetsUri,
            new CreateRuleSetRequest($"Otra {Guid.NewGuid():N}", null, RuleScope.Building, existing.ScopeKey),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resource_scope_with_unknown_resource_returns_400()
    {
        using var client = factory.CreateClient(Author);

        var response = await client.PostAsJsonAsync(
            RuleSetsUri,
            new CreateRuleSetRequest($"Equipo {Guid.NewGuid():N}", null, RuleScope.Resource, Guid.NewGuid().ToString()),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Should().ContainKey("ScopeKey");
    }

    [Fact]
    public async Task Resource_scope_with_existing_resource_is_created()
    {
        using var client = factory.CreateClient(Author);
        var resource = await client.CreateResourceAsync();

        var response = await client.PostAsJsonAsync(
            RuleSetsUri,
            new CreateRuleSetRequest($"Equipo {Guid.NewGuid():N}", null, RuleScope.Resource, resource.Id.ToString().ToUpperInvariant()),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<RuleSetResponse>()).ScopeKey.Should().Be(resource.Id.ToString());
    }

    [Fact]
    public async Task New_version_is_a_draft_and_keeps_rule_parameters()
    {
        using var client = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(client);

        var version = await AddVersionAsync(client, ruleSet.Id);

        version.Number.Should().Be(1);
        version.State.Should().Be(RuleSetVersionState.Draft);
        version.CreatedBy.Should().Be(Author);
        version.Rules.Should().HaveCount(2);
        version.Rules[0].Type.Should().Be(RuleType.MaxDuration);
        version.Rules[0].Parameters.GetProperty("maxMinutes").GetInt32().Should().Be(120);
    }

    [Fact]
    public async Task Approving_v2_retires_v1()
    {
        using var author = factory.CreateClient(Author);
        using var reviewer = factory.CreateClient(Reviewer);
        var ruleSet = await CreateBuildingRuleSetAsync(author);

        var v1 = await ApprovedVersionAsync(author, reviewer, ruleSet.Id);
        var v2 = await ApprovedVersionAsync(author, reviewer, ruleSet.Id);

        v1.State.Should().Be(RuleSetVersionState.Active);
        v2.State.Should().Be(RuleSetVersionState.Active);
        v2.ReviewedBy.Should().Be(Reviewer);

        var reloaded = await author.GetFromJsonAsync<RuleSetResponse>(
            new Uri($"/api/v1/rulesets/{ruleSet.Id}", UriKind.Relative), TestJson.Options);
        reloaded!.ActiveVersionNumber.Should().Be(2);
        reloaded.Versions.Select(v => v.State).Should().Equal(RuleSetVersionState.Retired, RuleSetVersionState.Active);
    }

    [Fact]
    public async Task Approving_a_draft_returns_409()
    {
        using var client = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(client);
        var draft = await AddVersionAsync(client, ruleSet.Id);

        var response = await PostVersionActionAsync(client, ruleSet.Id, draft.Number, "approve");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Title.Should().Be("Business rule violated");
    }

    [Fact]
    public async Task Reject_requires_a_comment_and_then_rejects()
    {
        using var author = factory.CreateClient(Author);
        using var reviewer = factory.CreateClient(Reviewer);
        var ruleSet = await CreateBuildingRuleSetAsync(author);
        var version = await AddVersionAsync(author, ruleSet.Id);
        await PostVersionActionAsync(author, ruleSet.Id, version.Number, "submit");

        var withoutComment = await PostVersionActionAsync(reviewer, ruleSet.Id, version.Number, "reject", new { comment = "" });
        var withComment = await PostVersionActionAsync(
            reviewer, ruleSet.Id, version.Number, "reject", new { comment = "Falta la regla de días festivos" });

        withoutComment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        withComment.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await withComment.ReadAsync<RuleSetVersionResponse>();
        rejected.State.Should().Be(RuleSetVersionState.Rejected);
        rejected.ReviewComment.Should().Be("Falta la regla de días festivos");
    }

    [Fact]
    public async Task Unknown_version_returns_404()
    {
        using var client = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(client);

        var response = await client.GetAsync(new Uri($"/api/v1/rulesets/{ruleSet.Id}/versions/9", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
