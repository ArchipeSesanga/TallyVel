namespace Api.Application.Contracts;

/// <summary>
/// Outcome of a registration attempt. A duplicate email is an expected
/// outcome, not an exception, so callers can map it to a 409.
/// </summary>
public sealed record RegisterResult(RegisterResponse? Response, bool IsDuplicateEmail)
{
    public bool IsSuccess => Response is not null;

    public static RegisterResult Success(RegisterResponse response) => new(response, false);
    public static RegisterResult DuplicateEmail() => new(null, true);
}

/// <summary>
/// Outcome of a login attempt. A wrong email and a wrong password are
/// deliberately the same outcome, so the API never reveals which emails exist.
/// </summary>
public sealed record LoginResult(LoginResponse? Response)
{
    public bool IsSuccess => Response is not null;

    public static LoginResult Success(LoginResponse response) => new(response);
    public static LoginResult InvalidCredentials() => new((LoginResponse?)null);
}
