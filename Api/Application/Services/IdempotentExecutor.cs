using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain;

namespace Api.Application.Services;

/// <summary>
/// Runs a request at most once per Idempotency-Key, the way ContributionServices does, for the endpoints
/// that follow it (join an invite, close a cycle):
///  - a missing key is a 400;
///  - the same key with the same request, after it finished, returns the ORIGINAL answer, with nothing done again;
///  - the same key with a different request is a 422;
///  - the same key arriving while the first is still running WAITS for it and then gets the same answer
///    (a double-click or a retry must not see an error just because it was fast);
///  - a request that fails releases its key, so the person can try again.
/// </summary>
public sealed class IdempotentExecutor(IIdempotencyStore store, ILogger<IdempotentExecutor> logger)
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(40);

    public async Task<T> RunAsync<T>(
        string? key, object requestIdentity, int successStatusCode, Func<Task<T>> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("An Idempotency-Key header is required.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestIdentity))));
        var waited = TimeSpan.Zero;

        while (true)
        {
            var (result, existing) = await store.TryReserveAsync(key, hash, ct);

            switch (result)
            {
                case ReserveResult.AlreadyCompleted:
                    logger.LogInformation("Replaying completed request for Idempotency-Key {IdempotencyKey}", key);
                    return JsonSerializer.Deserialize<T>(existing!.ResponseBodyJson!)!;

                case ReserveResult.PayloadMismatch:
                    throw new IdempotencyKeyReusedException(key);

                case ReserveResult.InProgress:
                    if (waited >= MaxWait) throw new IdempotencyKeyInProgressException(key);
                    await Task.Delay(PollEvery, ct);
                    waited += PollEvery;
                    continue;
            }

            // Reserved: this call owns the key. If anything fails, release it so a retry can run.
            try
            {
                var response = await action();
                await store.CompleteAsync(key, successStatusCode, JsonSerializer.Serialize(response), ct);
                return response;
            }
            catch
            {
                await store.ReleaseAsync(key, CancellationToken.None);
                throw;
            }
        }
    }
}
