using Asp.Versioning;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Resources;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/resources")]
[Produces("application/json")]
public sealed class ResourcesController(IResourceService resources) : ControllerBase
{
    private const string GetByIdRoute = "GetResourceById";

    [HttpGet]
    [ProducesResponseType<PagedResult<ResourceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ResourceResponse>>> List(
        [FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        Ok(await resources.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}", Name = GetByIdRoute)]
    [ProducesResponseType<ResourceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResourceResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await resources.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ResourceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResourceResponse>> Create(
        CreateResourceRequest request, CancellationToken cancellationToken)
    {
        var created = await resources.CreateAsync(request, cancellationToken);
        return CreatedAtRoute(GetByIdRoute, new { id = created.Id, version = "1" }, created);
    }
}
