using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CitSync.Rules.Application.Resources;
using CitSync.Rules.Domain.Resources;

namespace CitSync.Rules.IntegrationTests;

/// <summary>
/// Every test class in this collection shares ONE RulesApiFactory, so the SQL Server container
/// starts once per test run instead of once per class. Tests use unique names to avoid clashes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<RulesApiFactory>
{
    public const string Name = "api";
}

internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Options))!;

    public static async Task<ResourceResponse> CreateResourceAsync(this HttpClient client, int capacity = 10)
    {
        var request = new CreateResourceRequest($"Lab {Guid.NewGuid():N}", ResourceCategory.Lab, capacity, "Edificio A");
        var response = await client.PostAsJsonAsync(new Uri("/api/v1/resources", UriKind.Relative), request, Options);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<ResourceResponse>();
    }
}
