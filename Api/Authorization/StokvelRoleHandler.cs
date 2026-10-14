using Api.Observability;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Authorization;

/// <summary>
/// Checks the caller's role in the stokvel (the resource is the stokvel id
/// from the route). The role is read from the database on every call, so a
/// role change or removal takes effect immediately, not when the access
/// token expires.
/// </summary>
public sealed class StokvelRoleHandler(TallyVelDbContext db, TallyVelMetrics metrics)
    : AuthorizationHandler<StokvelRoleRequirement, Guid>
{
    public const string NotMember = "not-member";
    public const string MissingRole = "missing-role";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StokvelRoleRequirement requirement, Guid stokvelId)
    {
        if (!Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            context.Fail(new AuthorizationFailureReason(this, NotMember));
            return;
        }

        var role = await db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.StokvelId == stokvelId && m.UserId == userId)
            .Select(m => (Domain.MemberRole?)m.Role)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            metrics.AuthorizationRefused("not_found");
            context.Fail(new AuthorizationFailureReason(this, NotMember));
        }
        else if (!requirement.AllowedRoles.Contains(role.Value))
        {
            metrics.AuthorizationRefused("forbidden");
            context.Fail(new AuthorizationFailureReason(this, MissingRole));
        }
        else
            context.Succeed(requirement);
    }
}
