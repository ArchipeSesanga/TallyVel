using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Application.Contracts;
using Api.Application.Interfaces;

namespace Api.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _validator;

    public AuthController(IAuthService authService, IValidator<RegisterRequest> validator)
    {
        _authService = authService;
        _validator = validator;
    }

    [HttpPost("register")]
    [EndpointSummary("Register an account")]
    [EndpointDescription("Creates a new user account. The password is hashed before it is stored.")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var result = await _authService.RegisterAsync(request, ct);

        if (result.IsDuplicateEmail)
            return Problem(
                title: "Conflict",
                detail: "A user with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);

        // There is no GET-by-id for registrations to point at yet, so Created
        // links to the existing user endpoint.
        return CreatedAtAction(
            actionName: nameof(UserController.GetById),
            controllerName: "User",
            routeValues: new { id = result.Response!.Id },
            value: result.Response);
    }
}
