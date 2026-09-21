using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UserController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet]
    public ActionResult<IEnumerable<UserResponse>> GetAll() =>
        Ok(_userRepository.GetAll().Select(UserResponse.FromDomain));

    [HttpGet("{id:guid}")]
    public ActionResult<UserResponse> GetById(Guid id)
    {
        var user = _userRepository.GetById(id);
        if (user is null)
            return NotFound();

        return Ok(UserResponse.FromDomain(user));
    }

    [HttpPost]
    public ActionResult<UserResponse> Create(CreateUserRequest request)
    {
        User user;
        try
        {
            user = new User(request.Email, request.FullName, request.PasswordHash);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }

        _userRepository.Add(user);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, UserResponse.FromDomain(user));
    }
}
