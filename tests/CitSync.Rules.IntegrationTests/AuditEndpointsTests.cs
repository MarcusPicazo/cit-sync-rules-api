using System.Net.Http.Json;
using CitSync.Rules.Application.Auditing;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Domain.Auditing;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CitSync.Rules.IntegrationTests.RuleSetsEndpointsTests;

namespace CitSync.Rules.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class AuditEndpointsTests(RulesApiFactory factory)
{
    private static readonly DateTimeOffset Start = new(2030, 6, 3, 9, 0, 0, TimeSpan.FromHours(-6));

    private static async Task<IReadOnlyList<AuditEntryResponse>> AuditOfAsync(HttpClient client, string entity, Guid id)
    {
        var page = await client.GetFromJsonAsync<PagedResult<AuditEntryResponse>>(
            new Uri($"/api/v1/audit?entity={entity}&entityId={id}", UriKind.Relative), TestJson.Options);
        return page!.Items;
    }

    private static async Task<ReservationResponse> CreateReservationAsync(HttpClient client)
    {
        var resource = await client.CreateResourceAsync();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/reservations", UriKind.Relative),
            new CreateReservationRequest(resource.Id, "P0042", RequesterRole.Profesor, Start, Start.AddHours(2), 1, "Robótica"),
            TestJson.Options);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ReservationResponse>();
    }

    [Fact]
    public async Task Creating_a_rule_set_writes_a_Created_entry_with_the_actor()
    {
        using var client = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(client);

        var entries = await AuditOfAsync(client, "RuleSet", ruleSet.Id);

        var created = entries.Should().ContainSingle().Subject;
        created.Action.Should().Be(AuditAction.Created);
        created.Actor.Should().Be(Author);
        created.Before.Should().BeNull();
        created.After!.Value.GetProperty("Name").GetString().Should().Be(ruleSet.Name);
    }

    [Fact]
    public async Task Created_version_entry_includes_its_rules()
    {
        using var client = factory.CreateClient(Author);
        var ruleSet = await CreateBuildingRuleSetAsync(client);
        var version = await AddVersionAsync(client, ruleSet.Id);

        var entries = await AuditOfAsync(client, "RuleSetVersion", version.Id);

        var after = entries.Should().ContainSingle().Subject.After!.Value;
        after.GetProperty("Rules").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Approving_a_reservation_records_only_the_changed_values()
    {
        using var client = factory.CreateClient("admin.demo");
        var reservation = await CreateReservationAsync(client);

        await client.PostAsync(new Uri($"/api/v1/reservations/{reservation.Id}/approve", UriKind.Relative), null);

        var entries = await AuditOfAsync(client, "Reservation", reservation.Id);
        entries.Select(e => e.Action).Should().Equal(AuditAction.Updated, AuditAction.Created); // newest first

        var update = entries[0];
        update.Actor.Should().Be("admin.demo");
        update.Before!.Value.GetProperty("Status").GetString().Should().Be("Pending");
        update.After!.Value.GetProperty("Status").GetString().Should().Be("Approved");
        update.After.Value.TryGetProperty("Subject", out _).Should().BeFalse("unchanged values are not repeated");
    }

    [Fact]
    public async Task Request_without_X_Actor_is_audited_as_anonymous()
    {
        using var client = factory.CreateClient();
        var resource = await client.CreateResourceAsync();

        var entries = await AuditOfAsync(client, "Resource", resource.Id);

        entries.Should().ContainSingle().Which.Actor.Should().Be("anonymous");
    }

    [Fact(Skip = "TODO(Marcus): remove after implementing ResolveUpdateAction in AuditSaveChangesInterceptor")]
    public async Task Soft_delete_is_audited_as_Deleted()
    {
        using var client = factory.CreateClient("admin.demo");
        var reservation = await CreateReservationAsync(client);

        // There is no DELETE endpoint yet, so soft-delete through the DbContext (outside an HTTP request).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RulesDbContext>();
            var entity = await db.Reservations.SingleAsync(r => r.Id == reservation.Id);
            entity.SoftDelete(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var entries = await AuditOfAsync(client, "Reservation", reservation.Id);

        var latest = entries[0];
        latest.Action.Should().Be(AuditAction.Deleted);
        latest.Actor.Should().Be("system");
        latest.After!.Value.GetProperty("IsDeleted").GetBoolean().Should().BeTrue();
    }
}
