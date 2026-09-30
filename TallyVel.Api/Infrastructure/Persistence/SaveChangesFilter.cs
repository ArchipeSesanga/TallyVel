using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TallyVel.Api.Data;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Saves whatever the request changed on tracked entities, once the
/// action has finished successfully.
///
/// Why this exists: StokvelMembershipService loads a stokvel, calls
/// AddMember / RemoveMember / ChangeRole on it, and returns. The in-memory
/// repository never needed to hear about that, because it handed out the
/// very same object. With a database those changes go nowhere unless
/// something saves them, and IStokvelRepository has no Update or Save
/// method. This filter is that something — a unit of work per request.
///
/// Nothing is saved if the action threw or returned an error result, so a
/// rejected request never half-applies.
/// </summary>
public sealed class SaveChangesFilter : IAsyncActionFilter
{
    private readonly TallyVelDbContext _db;

    public SaveChangesFilter(TallyVelDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Canceled || executed.Exception is not null)
            return;

        if (executed.Result is IStatusCodeActionResult { StatusCode: >= 400 })
            return;

        if (_db.ChangeTracker.HasChanges())
        {
            // Not tied to the client's RequestAborted: once the work is
            // done, a disconnect shouldn't silently discard it.
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
