using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Microsoft.AspNetCore.RateLimiting;
using Api.Application.Contracts;
using Api.Application.Services;
using Api.Authorization;
using Domain;

namespace Api.Presentation.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/cycles")]
public class CyclesController : StokvelControllerBase
{
    private readonly ICycleRepository _cycles;
    private readonly PayoutService _payouts;
    private readonly CycleCloseService _closing;
    private readonly IValidator<OpenCycleRequest> _openValidator;
    private readonly IValidator<PayoutRequest> _payoutValidator;

    public CyclesController(
        ICycleRepository cycles,
        PayoutService payouts,
        CycleCloseService closing,
        IValidator<OpenCycleRequest> openValidator,
        IValidator<PayoutRequest> payoutValidator)
    {
        _cycles = cycles;
        _payouts = payouts;
        _closing = closing;
        _openValidator = openValidator;
        _payoutValidator = payoutValidator;
    }

    [HttpGet]
    [EndpointSummary("List a stokvel's contribution cycles")]
    [EndpointDescription("Any member of the stokvel can list its cycles.")]
    [ProducesResponseType<IEnumerable<CycleResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid stokvelId, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        var cycles = await _cycles.ListAsync(stokvelId, ct);
        return Ok(cycles.Select(CycleResponse.FromDomain));
    }

    [HttpPost]
    [EndpointSummary("Open a contribution cycle")]
    [EndpointDescription("Treasurer or Chairperson only.")]
    [ProducesResponseType<CycleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Open(Guid stokvelId, OpenCycleRequest request, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        var validation = await _openValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToModelState());

        var cycle = await _cycles.OpenAsync(stokvelId, request.Label, ct);
        return CreatedAtAction(nameof(List), new { stokvelId }, CycleResponse.FromDomain(cycle));
    }

    [HttpPost("{cycleId:guid}/payout")]
    [EnableRateLimiting(RateLimitPolicies.Money)]
    [EndpointSummary("Pay out a cycle")]
    [EndpointDescription("Pays the cycle's total contributions to one member and closes the cycle. Treasurer or Chairperson only.")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Payout(Guid stokvelId, Guid cycleId, PayoutRequest request, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        var validation = await _payoutValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToModelState());

        var payout = await _payouts.ProcessAsync(stokvelId, cycleId, request.RecipientUserId, ct);
        return StatusCode(StatusCodes.Status201Created, PayoutResponse.FromDomain(payout));
    }

    [HttpGet("{cycleId:guid}/close-preview")]
    [EndpointSummary("Preview closing a cycle")]
    [EndpointDescription(
        "Treasurer or Chairperson of THIS stokvel only (a member gets 403, a non-member 404). Shows who would be in arrears " +
        "if the cycle were closed now, counted up to asOfDate (the cycle's last day, or today if it is still running). " +
        "It uses the same calculation as the real close. Send the returned version back to close; it changes whenever anything " +
        "that affects the result changes. An empty arrears list is valid.")]
    [ProducesResponseType<ClosePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> ClosePreview(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        return Ok(await _closing.PreviewAsync(stokvelId, cycleId, ct));
    }

    [HttpPost("{cycleId:guid}/close")]
    [EnableRateLimiting(RateLimitPolicies.Money)]
    [EndpointSummary("Close a cycle")]
    [EndpointDescription(
        "Treasurer or Chairperson of THIS stokvel only. Body: the version from the preview. Requires an Idempotency-Key header. " +
        "If the version no longer matches a fresh calculation nothing is closed (409 close-preview-stale). If the cycle is already " +
        "closed, by the nightly job or another treasurer (409 cycle-already-closed). Otherwise it is closed exactly once, in one " +
        "transaction with the arrears rows. The same Idempotency-Key repeated returns the original 200 and records the arrears once.")]
    [ProducesResponseType<CloseCycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Close(Guid stokvelId, Guid cycleId, CloseCycleRequest request, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        if (string.IsNullOrWhiteSpace(request.Version))
            throw new ArgumentException("version is required: send the version from the close preview.");

        var key = Request.Headers["Idempotency-Key"].ToString();
        return Ok(await _closing.CloseAsync(stokvelId, cycleId, request.Version, key, ct));
    }
}
