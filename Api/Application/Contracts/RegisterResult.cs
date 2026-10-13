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
