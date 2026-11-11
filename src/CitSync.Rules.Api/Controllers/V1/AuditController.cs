using Asp.Versioning;
using CitSync.Rules.Application.Auditing;
using CitSync.Rules.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

/// <summary>Read-only audit trail, newest first. Example: ?entity=Reservation&amp;entityId={id}</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/audit")]
[Produces("application/json")]
public sealed class AuditController(IAuditService audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AuditEntryResponse>>> List(
        [FromQuery] AuditQuery query, CancellationToken cancellationToken) =>
        Ok(await audit.ListAsync(query, cancellationToken));
}
