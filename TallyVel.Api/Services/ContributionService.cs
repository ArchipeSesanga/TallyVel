using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TallyVel.Api.Common;
using TallyVel.Api.Controllers;
using TallyVel.Api.Data;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Services;

public class ContributionService
{
    private readonly IUserRepository _userRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IContributionRepository _contributionRepository;
    private readonly IIdempotencyStore _idempotencyStore;

    public ContributionService(
        IUserRepository userRepository,
        IStokvelRepository stokvelRepository,
        IContributionRepository contributionRepository,
        IIdempotencyStore idempotencyStore)
    {
        _userRepository = userRepository;
        _stokvelRepository = stokvelRepository;
        _contributionRepository = contributionRepository;
        _idempotencyStore = idempotencyStore;
    }

    public ContributionResponse RecordContribution(Guid stokvelId, RecordContributionRequest request, string? idempotencyKey)
    {
        // (a) An Idempotency-Key is mandatory, not optional. Recording a
        // contribution is a "pay someone" style write: if a client's
        // connection drops after we've saved it but before the client
        // sees our response, the client cannot tell "it worked" apart
        // from "it failed" — its only safe move is to retry. Without a
        // key supplied by the client, we have no way to recognise that
        // retry as the *same* request rather than a second, genuine
        // payment, so we refuse to proceed at all rather than guess.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("An Idempotency-Key header is required.", nameof(idempotencyKey));

        // We hash the inputs that define this request — not the
        // Contribution, which doesn't exist yet — so that reusing the
        // same key for a *different* request (a client bug) can be told
        // apart from a genuine retry of the *same* request.
        var payloadHash = HashPayload(new { stokvelId, request.MemberUserId, request.Cycle, request.Amount });

        // (b) The idempotency check runs before any lookups or domain
        // validation, and this ordering matters: if we validated first
        // and only consulted the idempotency store afterwards, a retried
        // request that originally succeeded could later fail for reasons
        // that have nothing to do with the retry itself (say, the member
        // was removed from the stokvel in the meantime). The caller would
        // then see a result that depends on when exactly the retry
        // landed — which defeats the point of idempotency. Checking the
        // key first guarantees a replay always returns exactly what the
        // original call returned, regardless of what's changed since.
        var existingRecord = _idempotencyStore.GetByKey(idempotencyKey);
        if (existingRecord is not null)
        {
            if (existingRecord.PayloadHash == payloadHash)
            {
                // Same key, same payload: a genuine retry. Hand back the
                // original response instead of creating a second
                // contribution — this is the whole point of the feature.
                return JsonSerializer.Deserialize<ContributionResponse>(existingRecord.ResponseBodyJson)!;
            }

            // Same key, different payload: the client has reused an
            // Idempotency-Key it already spent on a different request.
            // That's a misuse of the key, not a retry, so we reject it —
            // silently processing a different payment under an id that
            // promised idempotence would be worse than an outright error.
            throw new ConflictException(
                $"Idempotency-Key '{idempotencyKey}' was already used with a different request payload.");
        }

        // (c) Existence checks, stokvel before member. "Does this member
        // belong to this stokvel" is meaningless to ask before we even
        // know the stokvel and the member exist — asking it first could
        // turn a plain 404 into a confusing 422.
        var stokvel = _stokvelRepository.GetById(stokvelId)
            ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

        var member = _userRepository.GetById(request.MemberUserId)
            ?? throw new NotFoundException($"No user found with id {request.MemberUserId}.");

        // Both records exist, but that doesn't mean this user is allowed
        // to contribute to this particular stokvel. This is a rule about
        // an otherwise well-formed request, not a missing resource —
        // hence BusinessRuleViolationException (422), not NotFoundException.
        if (stokvel.Members.All(m => m.UserId != member.Id))
        {
            throw new BusinessRuleViolationException(
                $"User {member.Id} is not a member of stokvel {stokvelId}.");
        }

        // Only once we know the stokvel and the member are real, and
        // that the member actually belongs to this stokvel, does "have
        // they already paid for this cycle" become a meaningful
        // question — checking it any earlier risks answering a question
        // about a relationship that doesn't exist.
        if (_contributionRepository.ExistsForCycle(stokvelId, member.Id, request.Cycle))
        {
            throw new ConflictException(
                $"User {member.Id} has already contributed to stokvel {stokvelId} for cycle '{request.Cycle}'.");
        }

        // (d) Every precondition holds — this is the one place the
        // actual write happens.
        var contribution = new Contribution(stokvelId, member.Id, request.Cycle, request.Amount);
        _contributionRepository.Add(contribution);

        var response = ContributionResponse.FromDomain(contribution);

        // (e) The idempotency record is saved only *after* the write has
        // already succeeded — never before. If we saved it first and the
        // write then failed, a legitimate retry would find the key
        // already "used" and would replay a response for a contribution
        // that was never actually created. Saving it last guarantees the
        // key only ever stands for a request that truly completed.
        _idempotencyStore.Add(new IdempotencyRecord(idempotencyKey, payloadHash, JsonSerializer.Serialize(response)));

        // (f)
        return response;
    }

    private static string HashPayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
