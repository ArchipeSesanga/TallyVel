using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Api.Application.Contracts;
using Api.Application.Interfaces;
using Api.Application.Services;
using Api.Authorization;
using Domain;

namespace Api.Presentation.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/stokvels")]
public class StokvelController : StokvelControllerBase
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
    [EndpointSummary("List my stokvels")]
    [EndpointDescription("Returns the stokvels the signed-in user belongs to.")]
    [ProducesResponseType<IEnumerable<StokvelResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<StokvelResponse>> GetAll() =>
        Ok(_stokvelRepository.GetAll()
            .Where(s => s.Members.Any(m => m.UserId == CallerId))
            .Select(StokvelResponse.FromDomain));

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a stokvel by id")]
    [EndpointDescription("Returns a single stokvel, including its current members and their roles.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (await AuthorizeStokvelAsync(id, StokvelPolicies.Member) is { } denied) return denied;

        var stokvel = _stokvelRepository.GetById(id)
            ?? throw new NotFoundException("stokvel", id);

        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpPost]
    [EndpointSummary("Create a stokvel")]
    [EndpointDescription("Creates a new stokvel. The creator is automatically enrolled as its first member, with the Chairperson role.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Create(CreateStokvelRequest request)
    {
        // You can only create a stokvel for yourself; the creator becomes its Chairperson.
        if (request.CreatorId != CallerId)
            return Forbid();

        var validationResult = await _validator.ValidateAsync(request);
        Stokvel stokvel;

        if (!validationResult.IsValid)
        {

            // Return the model issues
            return ValidationProblem(validationResult.ToModelState());
        }
        try
        {
            stokvel = new Stokvel(request.Name, Money.FromCents(request.ContributionAmountCents), request.Cycle, request.CreatorId);
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
    [EndpointDescription("Enrolls a user into the stokvel with the given role. Chairperson only. Fails if the user is already a member.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> AddMember(Guid id, AddStokvelMemberRequest request)
    {
        if (await AuthorizeStokvelAsync(id, StokvelPolicies.Chairperson) is { } denied) return denied;

        var validationResult = await _addMemberValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(validationResult.ToModelState());

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
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        if (await AuthorizeStokvelAsync(id, StokvelPolicies.Chairperson) is { } denied) return denied;

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
    public async Task<IActionResult> ChangeMemberRole(Guid id, Guid userId, ChangeStokvelMemberRoleRequest request)
    {
        if (await AuthorizeStokvelAsync(id, StokvelPolicies.Chairperson) is { } denied) return denied;

        var validationResult = await _changeRoleValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(validationResult.ToModelState());

        // NotFoundException and BusinessRuleViolationException propagate to
        // TallyVelExceptionHandler, the only place that turns a domain
        // failure into a status code.
        var stokvel = _membershipService.ChangeRole(id, userId, request.Role);
        return Ok(StokvelResponse.FromDomain(stokvel));
    }
}
