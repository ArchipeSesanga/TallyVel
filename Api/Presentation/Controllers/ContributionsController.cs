using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Microsoft.AspNetCore.RateLimiting;
using Api.Application.Contracts;
using Api.Application.Interfaces;
using Api.Authorization;
using Domain;

namespace Api.Presentation.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/stokvels/{stokvelId:guid}/contributions")]
public class ContributionsController : StokvelControllerBase
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
    [EndpointSummary("List a stokvel's contributions, one page at a time")]
    [EndpointDescription(
        "Filters, sorting and paging all run in the database. pageSize defaults to 20 and is capped at 100; " +
        "sort is recordedAt (default) or amount, prefixed with '-' for descending. Pass nextPageToken back as " +
        "pageToken for the next page; an empty nextPageToken means there are no more results. " +
        "minAmountCents and maxAmountCents filter by amount, in integer cents.")]
    [ProducesResponseType<PagedResponse<ContributionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetPage(
        Guid stokvelId,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        [FromQuery] string? sort,
        [FromQuery] string? cycle,
        [FromQuery] Guid? memberUserId,
        [FromQuery] long? minAmountCents,
        [FromQuery] long? maxAmountCents,
        CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        // Both throw a TallyVelException on bad input → 400 via TallyVelExceptionHandler.
        var size = Paging.ResolvePageSize(pageSize);
        var (sortBy, descending) = contributionQuery.ParseSort(sort);

        var query = new contributionQuery(
            stokvelId, cycle?.Trim(), memberUserId,
            minAmountCents is { } min ? Money.FromCents(min) : null,
            maxAmountCents is { } max ? Money.FromCents(max) : null,
            sortBy, descending, size, pageToken);

        var (items, nextPageToken) = await _contributionRepository.GetPageAsync(query, ct);

        return Ok(new PagedResponse<ContributionResponse>(
            items.Select(ContributionResponse.FromDomain).ToList(), nextPageToken));
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a contribution by id")]
    [EndpointDescription("Returns a single contribution, scoped to this stokvel.")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid stokvelId, Guid id)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        var contribution = _contributionRepository.GetById(id);
        if (contribution is null || contribution.StokvelId != stokvelId)
            throw new NotFoundException("contribution", id);

        return Ok(ContributionResponse.FromDomain(contribution));
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Money)]
    [EndpointSummary("Record a contribution")]
    [EndpointDescription("Records a member's payment for a stokvel cycle. Requires an Idempotency-Key header, and rejects a second contribution from the same member for the same cycle.")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> RecordContribution(Guid stokvelId, RecordContributionRequest request, CancellationToken ct)
    {
        if (await AuthorizeStokvelAsync(stokvelId, StokvelPolicies.Member) is { } denied) return denied;

        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return ValidationProblem(validationResult.ToModelState());

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
