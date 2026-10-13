using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Application.Contracts;
using Api.Authorization;
using Domain;

namespace Api.Presentation.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class CyclesController : StokvelControllerBase
{
    private readonly ICycleRepository _cycles;
    private readonly IValidator<OpenCycleRequest> _openValidator;
    private readonly IValidator<PayoutRequest> _payoutValidator;

    public CyclesController(
        ICycleRepository cycles,
        IValidator<OpenCycleRequest> openValidator,
        IValidator<PayoutRequest> payoutValidator)
    {
        _cycles = cycles;
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
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var cycle = await _cycles.OpenAsync(stokvelId, request.Label, ct);
        return CreatedAtAction(nameof(List), new { stokvelId }, CycleResponse.FromDomain(cycle));
    }

    [HttpPost("{cycleId:guid}/payout")]
    [EndpointSummary("Pay out a cycle")]
    [EndpointDescription("Pays the cycle's total contributions to one member and closes the cycle. Treasurer or Chairperson only.")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Payout(Guid stokvelId, Guid cycleId, PayoutRequest request, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Treasurer) is { } denied) return denied;

        var validation = await _payoutValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var payout = await _cycles.PayOutAsync(stokvelId, cycleId, request.RecipientUserId, ct);
        return StatusCode(StatusCodes.Status201Created, PayoutResponse.FromDomain(payout));
    }
}
