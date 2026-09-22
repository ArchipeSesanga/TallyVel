using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Common;
using TallyVel.Api.Domain;
using TallyVel.Api.Services;

namespace TallyVel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContributionController : ControllerBase
{
    private readonly ContributionService _contributionService;

    public ContributionController(ContributionService contributionService)
    {
        _contributionService = contributionService;
    }

    [HttpPost]
    public ActionResult<ContributionResponse> CreateContribution(AddContributionRequest request)
    {
        Contribution contribution;
        try
        {
            contribution = _contributionService.AddContribution(request.StokvelId, request.ContributorId, request.Amount);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }

        return StatusCode(StatusCodes.Status201Created, ContributionResponse.FromDomain(contribution));
    }
}
