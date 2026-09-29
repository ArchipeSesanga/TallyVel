using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Presentation.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class ContributionsController : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionService _contributionService;
    private readonly IValidator<RecordContributionRequest> _validator;

    public ContributionsController(
        IContributionRepository contributionRepository,
        IContributionService contributionService,
        IValidator<RecordContributionRequest> validator)
    {
        _contributionRepository = contributionRepository;
        _contributionService = contributionService;
        _validator = validator;
    }

    [HttpGet]
    [EndpointSummary("List a stokvel's contributions")]
    [EndpointDescription("Returns every contribution recorded against this stokvel.")]
    [ProducesResponseType<IEnumerable<ContributionResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ContributionResponse>> GetAll(Guid stokvelId) =>
        Ok(_contributionRepository.GetAll()
            .Where(c => c.StokvelId == stokvelId)
            .Select(ContributionResponse.FromDomain));

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a contribution by id")]
    [EndpointDescription("Returns a single contribution, scoped to this stokvel.")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<ContributionResponse> GetById(Guid stokvelId, Guid id)
    {
        var contribution = _contributionRepository.GetById(id);
        if (contribution is null || contribution.StokvelId != stokvelId)
            throw new NotFoundException("contribution", id);

        return Ok(ContributionResponse.FromDomain(contribution));
    }

    [HttpPost]
    [EndpointSummary("Record a contribution")]
    [EndpointDescription("Records a member's payment for a stokvel cycle. Requires an Idempotency-Key header, and rejects a second contribution from the same member for the same cycle.")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
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

        // NotFoundException, BusinessRuleViolationException, AlreadyExistsException,
        // IdempotencyKeyReusedException and IdempotencyKeyInProgressException all
        // propagate to TallyVelExceptionHandler, the only place that turns a
        // domain failure into a status code.
        try
        {
            var response = await _contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey, ct);
            return CreatedAtAction(nameof(GetById), new { stokvelId, id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            // Missing Idempotency-Key header, or invalid domain input
            // (blank cycle, non-positive amount, etc.) — not one of ours,
            // so TallyVelExceptionHandler won't touch it. The request
            // itself is malformed, hence 400.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }
    }
}
