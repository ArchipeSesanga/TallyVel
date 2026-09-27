using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Application.Services;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Presentation.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class ContributionsController : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ContributionService _contributionService;
    private readonly IContributionRepository _contributionRepository;
    private readonly IValidator<RecordContributionRequest> _validator;

    public ContributionsController(
        ContributionService contributionService,
        IContributionRepository contributionRepository,
        IValidator<RecordContributionRequest> validator)
    {
        _contributionService = contributionService;
        _contributionRepository = contributionRepository;
        _validator = validator;
    }

    [HttpGet]
    public ActionResult<IEnumerable<ContributionResponse>> GetAll(Guid stokvelId) =>
        Ok(_contributionRepository.GetAll()
            .Where(c => c.StokvelId == stokvelId)
            .Select(ContributionResponse.FromDomain));

    [HttpGet("{id:guid}")]
    public ActionResult<ContributionResponse> GetById(Guid stokvelId, Guid id)
    {
        var contribution = _contributionRepository.GetById(id);
        if (contribution is null || contribution.StokvelId != stokvelId)
            return Problem(detail: $"No contribution found with id {id}.", statusCode: StatusCodes.Status404NotFound, title: "Not Found");

        return Ok(ContributionResponse.FromDomain(contribution));
    }

    [HttpPost]
    public async Task<ActionResult<ContributionResponse>> RecordContribution(Guid stokvelId, RecordContributionRequest request, CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));

        // The key travels as a header, not a body field, because it
        // identifies this *HTTP request attempt*, not the payment
        // itself — it's metadata about the call, which is what headers
        // are for, not part of the domain payload.
        var idempotencyKey = Request.Headers[IdempotencyKeyHeader].ToString();

        try
        {
            var response = await _contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey);
            return CreatedAtAction(nameof(GetById), new { stokvelId, id = response.Id }, response);
        }
        catch (NotFoundException ex)
        {
            // The stokvel or member referenced in the request doesn't exist.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (BusinessRuleViolationException ex)
        {
            // The request is well-formed but violates a domain rule
            // (e.g. the member isn't part of this stokvel) — 422, not 400,
            // because the request itself isn't malformed.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Business Rule Violation");
        }
        catch (ConflictException ex)
        {
            // Either the cycle is already paid for, or the Idempotency-Key
            // was reused with a different payload — both are conflicts
            // with existing state, hence 409.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
        }
        catch (ArgumentException ex)
        {
            // Covers both a missing Idempotency-Key and invalid domain
            // input (blank cycle, non-positive amount, etc.) — the
            // request itself is malformed, hence 400.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }
    }
}
