using Asp.Versioning;
using CitSync.Rules.Api.Http;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Reservations;
using Microsoft.AspNetCore.Mvc;

namespace CitSync.Rules.Api.Controllers.V1;

/// <summary>
/// Thin controller: HTTP in, call the use case, HTTP out. No business rules here;
/// errors bubble up as exceptions and ProblemDetailsExceptionHandler maps them.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/reservations")]
[Produces("application/json")]
public sealed class ReservationsController(IReservationService reservations) : ControllerBase
{
    private const string GetByIdRoute = "GetReservationById";

    [HttpGet]
    [ProducesResponseType<PagedResult<ReservationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReservationResponse>>> List(
        [FromQuery] ListReservationsQuery query, CancellationToken cancellationToken) =>
        Ok(await reservations.ListAsync(query, cancellationToken));

    [HttpGet("{id:guid}", Name = GetByIdRoute)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await reservations.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReservationResponse>> Create(
        CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var created = await reservations.CreateAsync(request, cancellationToken);
        return CreatedAtRoute(GetByIdRoute, new { id = created.Id, version = "1" }, created);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Approve(
        Guid id,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await reservations.ApproveAsync(id, IfMatchHeader.Parse(ifMatch), cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Reject(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        ChangeReservationStatusRequest? request,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await reservations.RejectAsync(
            id, request ?? new ChangeReservationStatusRequest(null), IfMatchHeader.Parse(ifMatch), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        ChangeReservationStatusRequest? request,
        [FromHeader(Name = IfMatchHeader.Name)] string? ifMatch,
        CancellationToken cancellationToken) =>
        Ok(await reservations.CancelAsync(
            id, request ?? new ChangeReservationStatusRequest(null), IfMatchHeader.Parse(ifMatch), cancellationToken));
}
