using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Domain;

namespace Infrastructure.Persistence;

/// <summary>
/// Stores refresh tokens in PostgreSQL. Scoped, like the other repositories.
/// </summary>
public sealed class EfRefreshTokenRepository(TallyVelDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken ct) =>
        db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    public async Task<bool> TryMarkUsedAsync(Guid id, Guid replacedBy, DateTimeOffset now, CancellationToken ct)
    {
        // Atomic: only succeeds if nobody else used it first
        var rows = await db.RefreshTokens
            .Where(t => t.Id == id && t.UsedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UsedAt, now)
                .SetProperty(t => t.ReplacedByTokenId, replacedBy), ct);
        return rows == 1;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
}
