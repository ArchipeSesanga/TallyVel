using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Api.Application.Contracts;
using Api.Application.Services;
using Api.Authorization;
using Api.Common;

namespace Api.Presentation.Controllers;

/// <summary>Create an invite for a stokvel you run.</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/invites")]
public class StokvelInvitesController(InviteService invites) : StokvelControllerBase
{
    [HttpPost]
    [EndpointSummary("Create an invite")]
    [EndpointDescription(
        "Treasurer or Chairperson of THIS stokvel only. Returns a code that works for 7 days. " +
        "Anyone signed in who has the code can preview the stokvel and join it.")]
    [ProducesResponseType<InviteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Create(Guid stokvelId, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        var invite = await invites.CreateAsync(stokvelId, CallerId, ct);
        return StatusCode(StatusCodes.Status201Created, invite);
    }
}

/// <summary>Use an invite code: preview the stokvel, then join it.</summary>
[ApiController]
[Route(ApiRoutes.V1 + "/invites/{code}")]
public class InvitesController(InviteService invites) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private Guid CallerId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.InviteGuess)]
    [EndpointSummary("Preview an invite")]
    [EndpointDescription(
        "What someone who may not be a member yet is allowed to see: the stokvel's name, rules, contribution amount, " +
        "frequency and cycle length, how many members it has, when the invite expires, and whether the caller is already in. " +
        "Nothing about its members, balances, contributions or payout order. Guesses are rate-limited per user.")]
    [ProducesResponseType<InvitePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<InvitePreviewResponse>> Preview(string code, CancellationToken ct) =>
        Ok(await invites.PreviewAsync(code, CallerId, ct));

    [HttpPost("join")]
    [EnableRateLimiting(RateLimitPolicies.InviteGuess)]
    [EndpointSummary("Join a stokvel with an invite")]
    [EndpointDescription(
        "Adds the caller as a Member. Requires an Idempotency-Key header: the same key replays the original 201, " +
        "and the database allows one membership per person per stokvel, so a double click or a retry never joins twice. " +
        "Already a member is a 409 (code already-member) with the stokvelId.")]
    [ProducesResponseType<JoinResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status410Gone, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Join(string code, CancellationToken ct)
    {
        var key = Request.Headers[IdempotencyKeyHeader].ToString();
        var joined = await invites.JoinAsync(code, CallerId, key, ct);
        return StatusCode(StatusCodes.Status201Created, joined);
    }
}
