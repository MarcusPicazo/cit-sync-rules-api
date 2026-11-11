using System.Net;
using System.Net.Http.Json;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Resources;
using CitSync.Rules.Domain.Resources;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class ResourcesEndpointsTests(RulesApiFactory factory)
{
    private static readonly Uri ResourcesUri = new("/api/v1/resources", UriKind.Relative);

    [Fact]
    public async Task Post_creates_resource_and_returns_201_with_location()
    {
        using var client = factory.CreateClient();
        var request = new CreateResourceRequest($"Aula {Guid.NewGuid():N}", ResourceCategory.Classroom, 40, "Edificio C");

        var response = await client.PostAsJsonAsync(ResourcesUri, request, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadAsync<ResourceResponse>();
        created.Category.Should().Be(ResourceCategory.Classroom);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/resources/{created.Id}");

        var fetched = await client.GetFromJsonAsync<ResourceResponse>(response.Headers.Location, TestJson.Options);
        fetched.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Post_with_invalid_body_returns_validation_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            ResourcesUri, new { name = "", category = "Lab", capacity = 0 }, TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().Contain(["Name", "Capacity"]);
    }

    [Fact]
    public async Task Post_duplicate_name_returns_409()
    {
        using var client = factory.CreateClient();
        var existing = await client.CreateResourceAsync();

        var response = await client.PostAsJsonAsync(
            ResourcesUri,
            new CreateResourceRequest(existing.Name, ResourceCategory.Lab, 5, null),
            TestJson.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri($"/api/v1/resources/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Title.Should().Be("Not found");
    }

    [Fact]
    public async Task Get_list_is_paginated()
    {
        using var client = factory.CreateClient();
        await client.CreateResourceAsync();
        await client.CreateResourceAsync();

        var page = await client.GetFromJsonAsync<PagedResult<ResourceResponse>>(
            new Uri("/api/v1/resources?page=1&pageSize=1", UriKind.Relative), TestJson.Options);

        page!.Items.Should().HaveCount(1);
        page.PageSize.Should().Be(1);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Get_list_with_page_size_too_big_returns_400()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/resources?pageSize=500", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
