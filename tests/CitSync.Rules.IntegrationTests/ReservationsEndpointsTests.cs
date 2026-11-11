using System.Net;
using System.Net.Http.Json;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Domain.Reservations;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class ReservationsEndpointsTests(RulesApiFactory factory)
{
    private static readonly Uri ReservationsUri = new("/api/v1/reservations", UriKind.Relative);

    // 9:00–11:00 Mexico City time.
    private static readonly DateTimeOffset Start = new(2030, 3, 4, 9, 0, 0, TimeSpan.FromHours(-6));

    private static CreateReservationRequest Request(Guid resourceId, int quantity = 1) => new(
        resourceId, "A01234", RequesterRole.Alumno, Start, Start.AddHours(2), quantity, "Programación");

    private static async Task<ReservationResponse> CreateAsync(HttpClient client, Guid resourceId)
    {
        var response = await client.PostAsJsonAsync(ReservationsUri, Request(resourceId), TestJson.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<ReservationResponse>();
    }

    private static Task<HttpResponseMessage> PostActionAsync(HttpClient client, Guid id, string action, object? body = null) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/reservations/{id}/{action}", UriKind.Relative), body, TestJson.Options);

    [Fact]
    public async Task Post_creates_pending_reservation_stored_in_utc()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();

        var created = await CreateAsync(client, resource.Id);

        created.Status.Should().Be(ReservationStatus.Pending);
        created.TimeZone.Should().Be("America/Mexico_City");
        created.StartLocal.Should().Be(Start);

        // Round-trip through SQL Server: the "Z"/UTC kind must survive.
        var fetched = await client.GetFromJsonAsync<ReservationResponse>(
            new Uri($"/api/v1/reservations/{created.Id}", UriKind.Relative), TestJson.Options);
        fetched!.StartUtc.Should().Be(new DateTime(2030, 3, 4, 15, 0, 0, DateTimeKind.Utc));
        fetched.StartUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Post_quantity_over_capacity_returns_400_on_Quantity()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync(capacity: 2);

        var response = await client.PostAsJsonAsync(ReservationsUri, Request(resource.Id, quantity: 5), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Should().ContainKey("Quantity");
    }

    [Fact]
    public async Task Post_unknown_resource_returns_400_on_ResourceId()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ReservationsUri, Request(Guid.NewGuid()), TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Should().ContainKey("ResourceId");
    }

    [Fact]
    public async Task Approve_then_approve_again_returns_409()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();
        var created = await CreateAsync(client, resource.Id);

        var first = await PostActionAsync(client, created.Id, "approve");
        var second = await PostActionAsync(client, created.Id, "approve");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.ReadAsync<ReservationResponse>()).Status.Should().Be(ReservationStatus.Approved);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.ReadAsync<ProblemDetails>()).Title.Should().Be("Business rule violated");
    }

    [Fact]
    public async Task Reject_with_reason_returns_rejected()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();
        var created = await CreateAsync(client, resource.Id);

        var response = await PostActionAsync(client, created.Id, "reject", new { reason = "Mantenimiento" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await response.ReadAsync<ReservationResponse>();
        rejected.Status.Should().Be(ReservationStatus.Rejected);
        rejected.StatusReason.Should().Be("Mantenimiento");
    }

    [Fact]
    public async Task Approve_unknown_reservation_returns_404()
    {
        using var client = factory.CreateClient();

        var response = await PostActionAsync(client, Guid.NewGuid(), "approve");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_list_filters_by_resource_and_status()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();
        var approved = await CreateAsync(client, resource.Id);
        await CreateAsync(client, resource.Id);
        await PostActionAsync(client, approved.Id, "approve");

        var page = await client.GetFromJsonAsync<PagedResult<ReservationResponse>>(
            new Uri($"/api/v1/reservations?resourceId={resource.Id}&status=Approved", UriKind.Relative),
            TestJson.Options);

        page!.Items.Should().ContainSingle().Which.Id.Should().Be(approved.Id);
    }

    [Fact]
    public async Task Cancel_approved_reservation_returns_cancelled()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();
        var created = await CreateAsync(client, resource.Id);
        await PostActionAsync(client, created.Id, "approve");

        var response = await PostActionAsync(client, created.Id, "cancel", new { reason = "Se suspendió la clase" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<ReservationResponse>()).Status.Should().Be(ReservationStatus.Cancelled);
    }
}
