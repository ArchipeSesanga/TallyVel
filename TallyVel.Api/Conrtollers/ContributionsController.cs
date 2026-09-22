using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Common;
using TallyVel.Api.Services;

namespace TallyVel.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class ContributionsController : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ContributionService _contributionService;

    public ContributionsController(ContributionService contributionService)
    {
        _contributionService = contributionService;
    }

    [HttpPost]
    public ActionResult<ContributionResponse> RecordContribution(Guid stokvelId, RecordContributionRequest request)
    {
        // The key travels as a header, not a body field, because it
        // identifies this *HTTP request attempt*, not the payment
        // itself — it's metadata about the call, which is what headers
        // are for, not part of the domain payload.
        var idempotencyKey = Request.Headers[IdempotencyKeyHeader].ToString();

        try
        {
            var response = _contributionService.RecordContribution(stokvelId, request, idempotencyKey);
            return StatusCode(StatusCodes.Status201Created, response);
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
