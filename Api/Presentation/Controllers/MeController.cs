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
public class MeController(MemberHomeService home, ActivityService activity, IUserRepository users) : ControllerBase
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

    [HttpGet("activity")]
    [EndpointSummary("My recent activity")]
    [EndpointDescription(
        "Contributions recorded, payouts made and members joined, across ONLY the stokvels the caller belongs to, " +
        "newest first. pageSize defaults to 20 and is capped at 100. Pass nextPageToken back as pageToken for the " +
        "next page; nextPageToken is an empty string (\"\") on the last page. occurredAt is UTC and ends in Z. " +
        "amountCents is null when no money is involved. personName is the member the line is about, and is null " +
        "once they are no longer in that stokvel.")]
    [ProducesResponseType<PagedResponse<ActivityItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<ActivityItem>>> Activity(
        [FromQuery] int? pageSize, [FromQuery] string? pageToken, CancellationToken ct) =>
        Ok(await activity.GetAsync(
            Guid.Parse(User.FindFirst("sub")!.Value), Paging.ResolvePageSize(pageSize), pageToken, ct));
}
