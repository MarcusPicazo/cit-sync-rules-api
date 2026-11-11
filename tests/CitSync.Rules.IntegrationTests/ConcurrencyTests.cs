using System.Net;
using System.Net.Http.Json;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.RuleSets;
using CitSync.Rules.Domain.Reservations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using static CitSync.Rules.IntegrationTests.RuleSetsEndpointsTests;

namespace CitSync.Rules.IntegrationTests;

/// <summary>
/// Optimistic concurrency end to end: the client sends back the "version" it read (If-Match);
/// if someone changed the row in between, the API answers 409 instead of overwriting.
/// </summary>
[Collection(ApiTestGroup.Name)]
public class ConcurrencyTests(RulesApiFactory factory)
{
    private static readonly DateTimeOffset Start = new(2030, 5, 6, 9, 0, 0, TimeSpan.FromHours(-6));

    private static HttpRequestMessage Post(string url, string ifMatch, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body, options: TestJson.Options),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        return request;
    }

    [Fact]
    public async Task Rule_set_version_with_stale_If_Match_returns_409()
    {
        using var author = factory.CreateClient(Author);
        using var reviewer = factory.CreateClient(Reviewer);
        var ruleSet = await CreateBuildingRuleSetAsync(author);
        var draft = await AddVersionAsync(author, ruleSet.Id);

        // The author submits using the version they read (valid)...
        var submit = await PostVersionActionAsync(author, ruleSet.Id, draft.Number, "submit", ifMatch: draft.Version);
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await submit.ReadAsync<RuleSetVersionResponse>();
        submitted.Version.Should().NotBe(draft.Version, "every UPDATE bumps the rowversion");

        // ...the reviewer still holds the old "version" (read before the submit) → stale.
        var stale = await PostVersionActionAsync(reviewer, ruleSet.Id, draft.Number, "approve", ifMatch: draft.Version);
        var fresh = await PostVersionActionAsync(reviewer, ruleSet.Id, draft.Number, "approve", ifMatch: submitted.Version);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.ReadAsync<ProblemDetails>()).Title.Should().Be("Concurrency conflict");
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reservation_cancel_with_stale_If_Match_returns_409_and_keeps_state()
    {
        using var client = factory.CreateClient("admin.demo");
        var resource = await client.CreateResourceAsync();
        var created = await (await client.PostAsJsonAsync(
                new Uri("/api/v1/reservations", UriKind.Relative),
                new CreateReservationRequest(resource.Id, "A00000", RequesterRole.Alumno, Start, Start.AddHours(1), 1, "Física"),
                TestJson.Options))
            .ReadAsync<ReservationResponse>();

        // Someone approves it (version changes)...
        (await client.SendAsync(Post($"/api/v1/reservations/{created.Id}/approve", created.Version)))
            .EnsureSuccessStatusCode();

        // ...and someone else tries to cancel with the version they read BEFORE the approval.
        var stale = await client.SendAsync(Post($"/api/v1/reservations/{created.Id}/cancel", created.Version, new { reason = "x" }));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var current = await client.GetFromJsonAsync<ReservationResponse>(
            new Uri($"/api/v1/reservations/{created.Id}", UriKind.Relative), TestJson.Options);
        current!.Status.Should().Be(ReservationStatus.Approved);
    }

    [Fact]
    public async Task Malformed_If_Match_returns_400()
    {
        using var client = factory.CreateClient("admin.demo");
        var resource = await client.CreateResourceAsync();
        var created = await (await client.PostAsJsonAsync(
                new Uri("/api/v1/reservations", UriKind.Relative),
                new CreateReservationRequest(resource.Id, "A00000", RequesterRole.Alumno, Start, Start.AddHours(1), 1, "Física"),
                TestJson.Options))
            .ReadAsync<ReservationResponse>();

        var response = await client.SendAsync(Post($"/api/v1/reservations/{created.Id}/approve", "%%not-base64%%"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Should().ContainKey("If-Match");
    }
}
