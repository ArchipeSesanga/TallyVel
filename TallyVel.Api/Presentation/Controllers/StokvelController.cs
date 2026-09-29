using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Application.Services;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Presentation.Controllers;

[ApiController]
[Route("api/stokvels")]
public class StokvelController : ControllerBase
{
    private readonly IStokvelRepository _stokvelRepository;
    private readonly StokvelMembershipService _membershipService;
    private readonly IValidator<CreateStokvelRequest> _validator;
    private readonly IValidator<AddStokvelMemberRequest> _addMemberValidator;
    private readonly IValidator<ChangeStokvelMemberRoleRequest> _changeRoleValidator;

    public StokvelController(
        IStokvelRepository stokvelRepository,
        StokvelMembershipService membershipService,
        IValidator<CreateStokvelRequest> validator,
        IValidator<AddStokvelMemberRequest> addMemberValidator,
        IValidator<ChangeStokvelMemberRoleRequest> changeRoleValidator)
    {
        _stokvelRepository = stokvelRepository;
        _membershipService = membershipService;
        _validator = validator;
        _addMemberValidator = addMemberValidator;
        _changeRoleValidator = changeRoleValidator;
    }

    [HttpGet]
    [EndpointSummary("List all stokvels")]
    [EndpointDescription("Returns every stokvel currently tracked, regardless of membership.")]
    [ProducesResponseType<IEnumerable<StokvelResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<StokvelResponse>> GetAll() =>
        Ok(_stokvelRepository.GetAll().Select(StokvelResponse.FromDomain));

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a stokvel by id")]
    [EndpointDescription("Returns a single stokvel, including its current members and their roles.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<StokvelResponse> GetById(Guid id)
    {
        var stokvel = _stokvelRepository.GetById(id)
            ?? throw new NotFoundException("stokvel", id);

        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpPost]
    [EndpointSummary("Create a stokvel")]
    [EndpointDescription("Creates a new stokvel. The creator is automatically enrolled as its first member, with the Admin role.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> Create(CreateStokvelRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        Stokvel stokvel;

        if (!validationResult.IsValid)
        {

            // Return the model issues
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }
        try
        {
            stokvel = new Stokvel(request.Name, request.ContributionAmount, request.Cycle, request.CreatorId);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }

        _stokvelRepository.Add(stokvel);

        return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, StokvelResponse.FromDomain(stokvel));
    }

    [HttpPost("{id:guid}/members")]
    [EndpointSummary("Add a member to a stokvel")]
    [EndpointDescription("Enrolls a user into the stokvel with the given role. Fails if the user is already a member.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> AddMember(Guid id, AddStokvelMemberRequest request)
    {
        var validationResult = await _addMemberValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));

        // NotFoundException and AlreadyExistsException propagate to
        // TallyVelExceptionHandler, the only place that turns a domain
        // failure into a status code.
        var stokvel = _membershipService.AddMember(id, request.UserId, request.Role);
        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [EndpointSummary("Remove a member from a stokvel")]
    [EndpointDescription("Removes the member. Refuses to remove the last admin, so a stokvel always has someone who can run it. Does not delete the member's past contributions.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public ActionResult<StokvelResponse> RemoveMember(Guid id, Guid userId)
    {
        // NotFoundException and BusinessRuleViolationException propagate to
        // TallyVelExceptionHandler, the only place that turns a domain
        // failure into a status code.
        var stokvel = _membershipService.RemoveMember(id, userId);
        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    [EndpointSummary("Change a member's role")]
    [EndpointDescription("Changes a member's role within the stokvel. Refuses a change that would leave the stokvel without an admin.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<StokvelResponse>> ChangeMemberRole(Guid id, Guid userId, ChangeStokvelMemberRoleRequest request)
    {
        var validationResult = await _changeRoleValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));

        // NotFoundException and BusinessRuleViolationException propagate to
        // TallyVelExceptionHandler, the only place that turns a domain
        // failure into a status code.
        var stokvel = _membershipService.ChangeRole(id, userId, request.Role);
        return Ok(StokvelResponse.FromDomain(stokvel));
    }
}
