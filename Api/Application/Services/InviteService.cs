using Api.Application.Contracts;
using Domain;
using Infrastructure.Data;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Services;

/// <summary>Creating, previewing and using stokvel invites. Every rule is read from the database.</summary>
public sealed class InviteService(
    TallyVelDbContext db, TimeProvider clock, IdempotentExecutor idempotency, ILogger<InviteService> logger)
{
    /// <summary>A code is 22 URL-safe characters. Anything else cannot exist, so it is "unknown" without asking the database.</summary>
    private static bool LooksLikeACode(string code) =>
        code.Length is >= 8 and <= 64 && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public async Task<InviteResponse> CreateAsync(Guid stokvelId, Guid createdByUserId, CancellationToken ct)
    {
        var invite = new StokvelInvite(stokvelId, createdByUserId, clock.GetUtcNow());
        db.StokvelInvites.Add(invite);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created an invite for stokvel {StokvelId}", stokvelId);
        return new InviteResponse(invite.Code, invite.ExpiresAt);
    }

    /// <summary>The invite, or 404 (unknown) / 410 (expired or withdrawn).</summary>
    private async Task<StokvelInvite> UsableInviteAsync(string code, CancellationToken ct)
    {
        var invite = LooksLikeACode(code)
            ? await db.StokvelInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, ct)
            : null;

        if (invite is null)
            throw new NotFoundException("invite", "that code", "No invite was found with that code.");

        if (!invite.IsUsableAt(clock.GetUtcNow()))
            throw new GoneException("invite-expired", "This invite has expired or was withdrawn. Ask for a new one.");

        return invite;
    }

    public async Task<InvitePreviewResponse> PreviewAsync(string code, Guid callerId, CancellationToken ct)
    {
        var invite = await UsableInviteAsync(code, ct);

        // Only the fields the preview is allowed to carry are selected: nothing about members, money paid or payouts.
        var stokvel = await db.Stokvels.AsNoTracking()
            .Where(s => s.Id == invite.StokvelId)
            .Select(s => new
            {
                s.Name, s.Rules, s.ContributionAmount, s.Cycle,
                MemberCount = s.Members.Count,
                AlreadyMember = s.Members.Any(m => m.UserId == callerId),
            })
            .SingleAsync(ct);

        return new InvitePreviewResponse(
            invite.Code,
            stokvel.Name,
            stokvel.Rules,
            Money.ToCents(stokvel.ContributionAmount),
            stokvel.Cycle,
            stokvel.Cycle switch
            {
                ContributionFrequency.Weekly => 7,
                ContributionFrequency.BiWeekly => 14,
                _ => null, // monthly: a calendar month is not a fixed number of days
            },
            stokvel.MemberCount,
            invite.ExpiresAt,
            stokvel.AlreadyMember);
    }

    public Task<JoinResponse> JoinAsync(string code, Guid callerId, string? idempotencyKey, CancellationToken ct) =>
        idempotency.RunAsync(idempotencyKey, new { code, callerId }, StatusCodes.Status201Created, async () =>
        {
            var invite = await UsableInviteAsync(code, ct);

            // Tracked, with its members, so the new member goes through the aggregate like every other membership.
            var stokvel = await db.Stokvels.Include(s => s.Members)
                .SingleAsync(s => s.Id == invite.StokvelId, ct);

            if (stokvel.Members.Any(m => m.UserId == callerId))
                throw AlreadyMember(stokvel.Id);

            stokvel.AddMember(callerId, MemberRole.Member);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Two joins at once, with different keys: the membership key (stokvel + user) let only one in.
                throw AlreadyMember(stokvel.Id);
            }

            logger.LogInformation("User {UserId} joined stokvel {StokvelId} with an invite", callerId, stokvel.Id);
            return new JoinResponse(stokvel.Id);
        }, ct);

    // The caller IS a member, so they may be told which stokvel it is.
    private static AlreadyExistsException AlreadyMember(Guid stokvelId) =>
        new("already-member", "You are already a member of this stokvel.")
        {
            Extensions = new Dictionary<string, object?> { ["stokvelId"] = stokvelId },
        };
}
