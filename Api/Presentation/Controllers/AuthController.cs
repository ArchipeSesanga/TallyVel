using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Api.Common;
using Api.Application.Contracts;
using Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Api.Presentation.Controllers;

[AllowAnonymous]
[ApiController]
[Route(ApiRoutes.V1 + "/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "tallyvel_rt";
    // Built from the same constant as the route, so they cannot drift apart.
    private const string CookiePath = "/" + ApiRoutes.V1 + "/auth";

    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    [HttpPost("register")]
    [EndpointSummary("Register an account")]
    [EndpointDescription("Creates a new user account. The password is hashed before it is stored.")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
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

    [HttpPost("login")]
    [EndpointSummary("Log in")]
    [EndpointDescription("Returns a short-lived access token in the body. The refresh token is set as an HttpOnly cookie and never appears in the body.")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        return ToResponse(await _authService.LoginAsync(request, ct));
    }

    [HttpPost("refresh")]
    [EndpointSummary("Refresh the access token")]
    [EndpointDescription("Reads the refresh cookie, exchanges it for a new access token and a new refresh cookie (rotation). Reusing an old cookie revokes the whole login.")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken ct) =>
        ToResponse(await _authService.RefreshAsync(Request.Cookies[RefreshCookieName], ct));

    [HttpPost("logout")]
    [EndpointSummary("Log out")]
    [EndpointDescription("Revokes the refresh cookie's login and clears the cookie. Always returns 204, so it is safe to call twice.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _authService.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        ClearRefreshCookie();
        return NoContent();
    }

    private ActionResult<AccessTokenResponse> ToResponse(AuthResult result)
    {
        if (!result.IsSuccess)
        {
            ClearRefreshCookie();
            return Problem(
                title: "Unauthorized",
                detail: result.Error,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var tokens = result.Tokens!;
        Response.Cookies.Append(RefreshCookieName, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,                      // JavaScript cannot read it, so XSS cannot steal it
            Secure = true,                        // HTTPS only
            SameSite = SameSiteMode.Strict,       // never sent on cross-site requests
            Path = CookiePath,                    // only sent to /api/v1/auth/*, not to every API call
            Expires = tokens.RefreshTokenExpiresAt
        });

        return Ok(new AccessTokenResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt));
    }

    // Must use the same Path as when the cookie was set, or the browser keeps it.
    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath
        });
}
