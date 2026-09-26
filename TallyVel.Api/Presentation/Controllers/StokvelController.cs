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

    public StokvelController(IStokvelRepository stokvelRepository, StokvelMembershipService membershipService)
    {
        _stokvelRepository = stokvelRepository;
        _membershipService = membershipService;
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
    public ActionResult<StokvelResponse> Create(CreateStokvelRequest request)
    {
        Stokvel stokvel;
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
    public ActionResult<StokvelResponse> AddMember(Guid id, AddStokvelMemberRequest request)
    {
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
    public ActionResult<StokvelResponse> ChangeMemberRole(Guid id, Guid userId, ChangeStokvelMemberRoleRequest request)
    {
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
