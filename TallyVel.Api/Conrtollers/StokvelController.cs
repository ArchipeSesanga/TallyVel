using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StokvelController : ControllerBase
{
    private readonly IStokvelRepository _stokvelRepository;

    public StokvelController(IStokvelRepository stokvelRepository)
    {
        _stokvelRepository = stokvelRepository;
    }

    [HttpGet]
    public ActionResult<IEnumerable<StokvelResponse>> GetAll() =>
        Ok(_stokvelRepository.GetAll().Select(StokvelResponse.FromDomain));

    [HttpGet("{id:guid}")]
    public ActionResult<StokvelResponse> GetById(Guid id)
    {
        var stokvel = _stokvelRepository.GetById(id);
        if (stokvel is null)
            return NotFound();

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
            return ValidationProblem(ex.Message);
        }

        _stokvelRepository.Add(stokvel);

        return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, StokvelResponse.FromDomain(stokvel));
    }

    [HttpPost("{id:guid}/members")]
    public ActionResult<StokvelResponse> AddMember(Guid id, AddStokvelMemberRequest request)
    {
        var stokvel = _stokvelRepository.GetById(id);
        if (stokvel is null)
            return NotFound();

        try
        {
            stokvel.AddMember(request.UserId, request.Role);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public ActionResult<StokvelResponse> RemoveMember(Guid id, Guid userId)
    {
        var stokvel = _stokvelRepository.GetById(id);
        if (stokvel is null)
            return NotFound();

        try
        {
            stokvel.RemoveMember(userId);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        return Ok(StokvelResponse.FromDomain(stokvel));
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    public ActionResult<StokvelResponse> ChangeMemberRole(Guid id, Guid userId, ChangeStokvelMemberRoleRequest request)
    {
        var stokvel = _stokvelRepository.GetById(id);
        if (stokvel is null)
            return NotFound();

        try
        {
            stokvel.ChangeRole(userId, request.Role);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        return Ok(StokvelResponse.FromDomain(stokvel));
    }
}
