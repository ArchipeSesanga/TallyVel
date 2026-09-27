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
    public ActionResult<IEnumerable<StokvelResponse>> GetAll() =>
        Ok(_stokvelRepository.GetAll().Select(StokvelResponse.FromDomain));

    [HttpGet("{id:guid}")]
    public ActionResult<StokvelResponse> GetById(Guid id)
    {
        var stokvel = _stokvelRepository.GetById(id);
        if (stokvel is null)
            return Problem(detail: $"No stokvel found with id {id}.", statusCode: StatusCodes.Status404NotFound, title: "Not Found");

        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(CreateStokvelRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        Stokvel stokvel;

        if (!validationResult.IsValid)
        {
            
        // Return the model issues
            return  ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
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
    public async Task<ActionResult<StokvelResponse>> AddMember(Guid id, AddStokvelMemberRequest request)
    {
        var validationResult = await _addMemberValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));

        try
        {
            var stokvel = _membershipService.AddMember(id, request.UserId, request.Role);
            return Ok(StokvelResponse.FromDomain(stokvel));
        }
        catch (NotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (ConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
        }
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public ActionResult<StokvelResponse> RemoveMember(Guid id, Guid userId)
    {
        try
        {
            var stokvel = _membershipService.RemoveMember(id, userId);
            return Ok(StokvelResponse.FromDomain(stokvel));
        }
        catch (NotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (BusinessRuleViolationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Business Rule Violation");
        }
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    public async Task<ActionResult<StokvelResponse>> ChangeMemberRole(Guid id, Guid userId, ChangeStokvelMemberRoleRequest request)
    {
        var validationResult = await _changeRoleValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));

        try
        {
            var stokvel = _membershipService.ChangeRole(id, userId, request.Role);
            return Ok(StokvelResponse.FromDomain(stokvel));
        }
        catch (NotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (BusinessRuleViolationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Business Rule Violation");
        }
    }
}
