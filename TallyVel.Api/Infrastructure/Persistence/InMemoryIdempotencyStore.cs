using System.Collections.Concurrent;
using TallyVel.Api.Application.Interfaces;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Holds used Idempotency-Keys in memory instead of a database. Same
/// rationale — and same limitation, it doesn't survive a restart or
/// work across multiple instances — as <see cref="InMemoryContributionRepository"/>.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new();

    public Task<(ReserveResult, IdempotencyRecord?)> TryReserveAsync(
        string key, string payloadHash, CancellationToken ct = default)
    {
        var fresh = new IdempotencyRecord(key, payloadHash, DateTime.UtcNow);
        if (_records.TryAdd(key, fresh))
            return Task.FromResult((ReserveResult.Reserved, (IdempotencyRecord?)null));

        var existing = _records[key];
        var result = existing.PayloadHash != payloadHash ? ReserveResult.PayloadMismatch
                   : existing.StatusCode is null         ? ReserveResult.InProgress
                   :                                       ReserveResult.AlreadyCompleted;
        return Task.FromResult((result, (IdempotencyRecord?)existing));
    }

    public Task CompleteAsync(string key, int statusCode, string body, CancellationToken ct = default)
    {
        _records[key] = _records[key] with { StatusCode = statusCode, ResponseBodyJson = body };
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken ct = default)
    {
        _records.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
