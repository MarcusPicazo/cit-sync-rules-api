using Asp.Versioning;
using CitSync.Rules.Application.Rules;
using CitSync.Rules.Application.Rules.Engine;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rules")]
[Produces("application/json")]
public sealed class RulesController(IRuleEngine engine) : ControllerBase
{
    /// <summary>
    /// Dry run: would this reservation be allowed by the Active rules that apply to the resource?
    /// Always 200 with { allowed, violations[] } — "not allowed" is a valid answer, not an error.
    /// 400 only when the request itself is malformed (or would fail POST /reservations' own checks).
    /// Nothing is saved.
    /// </summary>
    [HttpPost("evaluate")]
    [Consumes("application/json")]
    [ProducesResponseType<EvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvaluationResponse>> Evaluate(
        EvaluateReservationRequest request, CancellationToken cancellationToken) =>
        Ok(await engine.EvaluateAsync(request, cancellationToken));
}
