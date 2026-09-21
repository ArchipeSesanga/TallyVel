using TallyVel.Api.Domain;

namespace TallyVel.Api.Controllers;

/// <summary>
/// What a User looks like over the wire — deliberately excludes
/// PasswordHash so it can never leak into a response body.
/// </summary>
public sealed record UserResponse(Guid Id, string Email, string FullName, DateTimeOffset CreatedAt)
{
    public static UserResponse FromDomain(User user) =>
        new(user.Id, user.Email, user.FullName, user.CreatedAt);
}

public sealed record CreateUserRequest(string Email, string FullName, string PasswordHash);
