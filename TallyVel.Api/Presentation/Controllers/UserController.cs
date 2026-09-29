using FluentValidation;
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
    private readonly IValidator<CreateUserRequest> _validator;

    public UserController(IUserRepository userRepository, IValidator<CreateUserRequest> validator)
    {
        _userRepository = userRepository;
        _validator = validator;
    }

    [HttpGet]
    [EndpointSummary("List all users")]
    [EndpointDescription("Returns every registered user.")]
    [ProducesResponseType<IEnumerable<UserResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<UserResponse>> GetAll() =>
        Ok(_userRepository.GetAll().Select(UserResponse.FromDomain));

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a user by id")]
    [EndpointDescription("Returns a single user.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<UserResponse> GetById(Guid id)
    {
        var user = _userRepository.GetById(id)
            ?? throw new NotFoundException("user", id);

        return Ok(UserResponse.FromDomain(user));
    }

    [HttpPost]
    [EndpointSummary("Register a user")]
    [EndpointDescription("Creates a new user account.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
{

    //validation line
    var result = await _validator.ValidateAsync(request, ct);
    if (!result.IsValid)
        return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));


    // logic for the request 
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
