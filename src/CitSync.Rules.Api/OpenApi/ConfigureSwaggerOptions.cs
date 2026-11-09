using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CitSync.Rules.Api.OpenApi;

/// <summary>
/// Creates one Swagger document per API version discovered by Asp.Versioning (v1 now, v2 later).
/// </summary>
internal sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "CIT-Sync Scheduling Rules API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? "This API version is deprecated."
                    : "Scheduling rules, reservations and approvals for CIT-Sync.",
            });
        }
    }
}
