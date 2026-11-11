using Asp.Versioning;
using CitSync.Rules.Api.Http;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.RuleSets;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

/// <summary>
/// Rule sets and their versions. Workflow: POST versions (Draft) → submit → approve | reject.
/// Approving a version retires the previously active one. Send X-Actor to identify yourself.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rulesets")]
[Produces("application/json")]
public sealed class RuleSetsController(IRuleSetService ruleSets) : ControllerBase
{
    private const string GetByIdRoute = "GetRuleSetById";
    private const string GetVersionRoute = "GetRuleSetVersion";

    [HttpGet]
    [ProducesResponseType<PagedResult<RuleSetSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RuleSetSummaryResponse>>> List(
        [FromQuery] ListRuleSetsQuery query, CancellationToken cancellationToken) =>
        Ok(await ruleSets.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}", Name = GetByIdRoute)]
    [ProducesResponseType<RuleSetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleSetResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await ruleSets.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<RuleSetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetResponse>> Create(
        CreateRuleSetRequest request, CancellationToken cancellationToken)
    {
        var created = await ruleSets.CreateAsync(request, cancellationToken);
        return CreatedAtRoute(GetByIdRoute, new { id = created.Id, version = "1" }, created);
    }

    [HttpGet("{id:guid}/versions/{number:int}", Name = GetVersionRoute)]
    [ProducesResponseType<RuleSetVersionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleSetVersionResponse>> GetVersion(
        Guid id, int number, CancellationToken cancellationToken) =>
        Ok(await ruleSets.GetVersionAsync(id, number, cancellationToken));

    [HttpPost("{id:guid}/versions")]
    [Consumes("application/json")]
    [ProducesResponseType<RuleSetVersionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetVersionResponse>> AddVersion(
        Guid id, CreateRuleSetVersionRequest request, CancellationToken cancellationToken)
    {
        var created = await ruleSets.AddVersionAsync(id, request, cancellationToken);
        return CreatedAtRoute(GetVersionRoute, new { id, number = created.Number, version = "1" }, created);
    }

    [HttpPost("{id:guid}/versions/{number:int}/submit")]
    [ProducesResponseType<RuleSetVersionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetVersionResponse>> Submit(
        Guid id,
        int number,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await ruleSets.SubmitAsync(id, number, IfMatchHeader.Parse(ifMatch), cancellationToken));

    [HttpPost("{id:guid}/versions/{number:int}/approve")]
    [ProducesResponseType<RuleSetVersionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetVersionResponse>> Approve(
        Guid id,
        int number,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await ruleSets.ApproveAsync(id, number, IfMatchHeader.Parse(ifMatch), cancellationToken));

    [HttpPost("{id:guid}/versions/{number:int}/reject")]
    [Consumes("application/json")]
    [ProducesResponseType<RuleSetVersionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleSetVersionResponse>> Reject(
        Guid id,
        int number,
        RejectRuleSetVersionRequest request,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await ruleSets.RejectAsync(id, number, request, IfMatchHeader.Parse(ifMatch), cancellationToken));
}
