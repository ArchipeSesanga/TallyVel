using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Api.Application.Contracts;
using Api.Application.Interfaces;
using Api.Application.Services;
using Domain;

namespace Api.Presentation.Controllers;

/// <summary>Screens about the signed-in user. Requires a token (fallback policy).</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/me")]
public class MeController(MemberHomeService home, IUserRepository users) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Who am I")]
    [EndpointDescription(
        "The signed-in member: id, email and full name. Never includes the password hash. " +
        "Their role is not here because a role belongs to one stokvel; /me/home returns it per stokvel.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public ActionResult<UserResponse> Get()
    {
        var id = Guid.Parse(User.FindFirst("sub")!.Value);
        // A valid token for a user that no longer exists is "not found", not a crash.
        var user = users.GetById(id) ?? throw new NotFoundException("user", id);
        return Ok(UserResponse.FromDomain(user));
    }

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
