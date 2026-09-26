namespace TallyVel.Api.Application.Interfaces;

public enum ReserveResult
{
    Reserved,
    AlreadyCompleted,
    InProgress,
    PayloadMismatch
}

public sealed record IdempotencyRecord(
    string Key,
    string PayloadHash,
    DateTime CreatedAt,
    int? StatusCode = null,          // null = still processing
    string? ResponseBodyJson = null);

public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically claims the key. If it already exists, reports why and
    /// returns the stored record so a completed response can be replayed.
    /// </summary>
    Task<(ReserveResult Result, IdempotencyRecord? Existing)> TryReserveAsync(
        string key, string payloadHash, CancellationToken ct = default);

    Task CompleteAsync(string key, int statusCode, string responseBodyJson, CancellationToken ct = default);

    Task ReleaseAsync(string key, CancellationToken ct = default);
}