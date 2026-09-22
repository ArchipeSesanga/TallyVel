using System.Collections.Concurrent;

namespace TallyVel.Api.Data;

/// <summary>
/// Holds used Idempotency-Keys in memory instead of a database. Same
/// rationale — and same limitation, it doesn't survive a restart or
/// work across multiple instances — as <see cref="InMemoryContributionRepository"/>.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new();

    public IdempotencyRecord? GetByKey(string key) => _records.GetValueOrDefault(key);

    public void Add(IdempotencyRecord record)
    {
        if (!_records.TryAdd(record.Key, record))
            throw new InvalidOperationException($"Idempotency key '{record.Key}' has already been recorded.");
    }
}
