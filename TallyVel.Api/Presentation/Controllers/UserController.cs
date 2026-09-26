using Microsoft.AspNetCore.Mvc;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Presentation.Controllers;

[ApiController]
[Route("api/users")]
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
            return Problem(detail: $"No user found with id {id}.", statusCode: StatusCodes.Status404NotFound, title: "Not Found");

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
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
        }

        _userRepository.Add(user);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, UserResponse.FromDomain(user));
    }
}
