using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Api.Application.Contracts;
using Api.Application.Services;

namespace Api.Presentation.Controllers;

/// <summary>Screens about the signed-in user. Requires a token (fallback policy).</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/me")]
public class MeController(MemberHomeService home) : ControllerBase
{
    [HttpGet("home")]
    [EndpointSummary("Member home screen")]
    [EndpointDescription(
        "One card per stokvel the caller belongs to: their role, the current cycle, what they have paid and still owe " +
        "this cycle (integer cents), and the next payout date (a South African calendar date). currentCycle is an " +
        "object with its dates (endsOn is the due date), or null when no cycle is open; paid and owed are then 0. " +
        "An empty list if they belong to none.")]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken ct) =>
        Ok(await home.GetAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct));
}
