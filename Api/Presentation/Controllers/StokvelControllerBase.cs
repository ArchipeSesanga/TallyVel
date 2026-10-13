using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Presentation.Controllers;

/// <summary>
/// Shared by every controller under /api/stokvels/{stokvelId}/...
/// </summary>
public abstract class StokvelControllerBase : ControllerBase
{
    /// <summary>The signed-in user's id, from the token's "sub" claim.</summary>
    protected Guid CallerId => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>
    /// Returns null when the caller may proceed; otherwise the response to return.
    /// Not a member → 404, so the API does not confirm the stokvel exists.
    /// A member without the needed role → 403.
    /// </summary>
    protected async Task<IActionResult?> AuthorizeStokvelAsync(Guid stokvelId, string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = await authorization.AuthorizeAsync(User, stokvelId, policy);

        if (result.Succeeded)
            return null;

        var reasons = result.Failure?.FailureReasons.Select(r => r.Message) ?? [];
        return reasons.Contains(StokvelRoleHandler.MissingRole) ? Forbid() : NotFound();
    }
}
