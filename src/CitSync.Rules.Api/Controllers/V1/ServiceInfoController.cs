using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/info")]
public sealed class ServiceInfoController : ControllerBase
{
    public sealed record ServiceInfoResponse(string Service, string Version, string Environment);

    [HttpGet]
    [ProducesResponseType<ServiceInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<ServiceInfoResponse> Get(
        [FromServices] IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var version = typeof(ServiceInfoController).Assembly.GetName().Version?.ToString() ?? "unknown";
        return Ok(new ServiceInfoResponse("cit-sync-rules-api", version, environment.EnvironmentName));
    }
}
