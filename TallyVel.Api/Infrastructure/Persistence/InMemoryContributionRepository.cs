using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Domain;

namespace TallyVel.Api.Infrastructure.Persistence;

/// <summary>
/// Holds Contributions in memory instead of a database. Same rationale
/// as <see cref="InMemoryStokvelRepository"/>.
/// </summary>
public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentDictionary<Guid, Contribution> _contributions = new();
    private readonly IUserRepository _userRepository;
    private readonly IStokvelRepository _stokvelRepository;
    private readonly IIdempotencyStore _idempotencyStore;

    public InMemoryContributionRepository(
        IUserRepository userRepository,
        IStokvelRepository stokvelRepository,
        IIdempotencyStore idempotencyStore)
    {
        _userRepository = userRepository;
        _stokvelRepository = stokvelRepository;
        _idempotencyStore = idempotencyStore;
    }

    public IReadOnlyCollection<Contribution> GetAll() => _contributions.Values.ToList().AsReadOnly();

    public Contribution? GetById(Guid id) => _contributions.GetValueOrDefault(id);

    public Task AddAsync(Contribution contribution)
    {
        if (!_contributions.TryAdd(contribution.Id, contribution))
            throw new InvalidOperationException($"A contribution with id {contribution.Id} already exists.");

            return Task.CompletedTask;
    }

    public bool ExistsForCycle(Guid stokvelId, Guid memberUserId, string cycle) =>
        _contributions.Values.Any(c =>
            c.StokvelId == stokvelId &&
            c.MemberUserId == memberUserId &&
            string.Equals(c.Cycle, cycle.Trim(), StringComparison.Ordinal));

    public async Task<ContributionResponse> RecordContributionAsync(
        Guid stokvelId,
        RecordContributionRequest request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        // (a) Key is mandatory: without it a retry is indistinguishable
        // from a second, genuine payment.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("An Idempotency-Key header is required.", nameof(idempotencyKey));

        var payloadHash = HashPayload(new { stokvelId, request.MemberUserId, request.Cycle, request.Amount });

        // (b) Atomically claim the key before doing anything else. Unlike
        // "check, then add at the end", this closes the window where two
        // concurrent requests with the same key could both see "unused"
        // and both record a contribution.
        var (result, existing) = await _idempotencyStore.TryReserveAsync(idempotencyKey, payloadHash, ct);

        switch (result)
        {
            case ReserveResult.AlreadyCompleted:
                // Genuine retry of a finished request: replay the original.
                return JsonSerializer.Deserialize<ContributionResponse>(existing!.ResponseBodyJson!)!;

            case ReserveResult.InProgress:
                // Same key, same payload, but the first attempt hasn't
                // finished yet. Tell the client to retry shortly rather
                // than processing it a second time.
                throw new ConflictException(
                    $"A request with Idempotency-Key '{idempotencyKey}' is still being processed. Retry shortly.");

            case ReserveResult.PayloadMismatch:
                throw new ConflictException(
                    $"Idempotency-Key '{idempotencyKey}' was already used with a different request payload.");
        }

        // From here on we own the key. If anything fails, release it so
        // the client can retry; a key must only ever stand for a request
        // that truly completed.
        try
        {
            // (c) Existence checks, stokvel before member.
            var stokvel = _stokvelRepository.GetById(stokvelId)
                ?? throw new NotFoundException($"No stokvel found with id {stokvelId}.");

            var member = _userRepository.GetById(request.MemberUserId)
                ?? throw new NotFoundException($"No user found with id {request.MemberUserId}.");

            if (stokvel.Members.All(m => m.UserId != member.Id))
            {
                throw new BusinessRuleViolationException(
                    $"User {member.Id} is not a member of stokvel {stokvelId}.");
            }

            if (ExistsForCycle(stokvelId, member.Id, request.Cycle))
            {
                throw new ConflictException(
                    $"User {member.Id} has already contributed to stokvel {stokvelId} for cycle '{request.Cycle}'.");
            }

            // (d) The one place the actual write happens.
            var contribution = new Contribution(stokvelId, member.Id, request.Cycle, request.Amount);
            await AddAsync(contribution);

            var response = ContributionResponse.FromDomain(contribution);

            // (e) Mark the key completed with the exact response, so
            // retries replay it verbatim (same Id, same RecordedAt).
            await _idempotencyStore.CompleteAsync(
                idempotencyKey, StatusCodes.Status201Created, JsonSerializer.Serialize(response), ct);

            return response;
        }
        catch
        {
            await _idempotencyStore.ReleaseAsync(idempotencyKey, CancellationToken.None);
            throw;
        }
    }

    private static string HashPayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    public Task UpdateAsync(Contribution contribution)
    {
        // addValueFactory only runs if the key is absent, so this fails
        // fast instead of silently inserting under the name "update".
        _contributions.AddOrUpdate(
            contribution.Id,
            addValueFactory: _ => throw new NotFoundException($"No contribution found with id {contribution.Id}."),
            updateValueFactory: (_, _) => contribution);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        throw new NotImplementedException();
    }
}
