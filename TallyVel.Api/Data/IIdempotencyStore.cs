namespace TallyVel.Api.Data;

/// <summary>
/// One previously-processed idempotent request: the client-supplied key,
/// a hash of the payload that key was used with, and the exact response
/// body that was returned. Storing the actual response — not just "this
/// key was used" — is what lets a retried request get back the original
/// result verbatim instead of a freshly recomputed one that could differ
/// in ways the client would notice (a new Id, a new RecordedAt, etc.).
/// </summary>
public sealed record IdempotencyRecord(string Key, string PayloadHash, string ResponseBodyJson);

/// <summary>
/// An abstraction over where used Idempotency-Keys are tracked. Same
/// rationale as <see cref="IContributionRepository"/> — this is the seam
/// that would let a durable, shared store (required once this API runs
/// on more than one instance, since separate processes can't see each
/// other's in-memory dictionaries) replace the in-memory one without
/// touching ContributionService.
/// </summary>
public interface IIdempotencyStore
{
    IdempotencyRecord? GetByKey(string key);
    void Add(IdempotencyRecord record);
}
