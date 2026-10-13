namespace Domain;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken ct);

    /// <summary>
    /// Atomically marks a token as used. Returns false if it was already
    /// used or revoked, i.e. another request got there first.
    /// </summary>
    Task<bool> TryMarkUsedAsync(Guid id, Guid replacedBy, DateTimeOffset now, CancellationToken ct);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct);
}
