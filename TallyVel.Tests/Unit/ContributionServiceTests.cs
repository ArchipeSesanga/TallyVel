using Microsoft.Extensions.Logging.Abstractions;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Application.Interfaces;
using TallyVel.Api.Application.Services;
using TallyVel.Api.Domain;
using TallyVel.Api.Infrastructure.Persistence;

namespace TallyVel.Tests.Unit;

public class ContributionServiceTests
{
    // Shared setup: xUnit creates a NEW instance of this class for every
    // test, so each test gets fresh, empty repositories. No test can
    // affect another.
    private readonly User _admin;
    private readonly User _outsider;
    private readonly Stokvel _stokvel;
    private readonly IContributionRepository _contributions;
    private readonly IContributionService _service;

    public ContributionServiceTests()
    {
        _admin = new User("admin@test.com", "Admin User", "hash");
        _outsider = new User("outsider@test.com", "Outsider User", "hash");

        // The creator is automatically the stokvel's first member (Admin).
        _stokvel = new Stokvel("Test Stokvel", 500m, ContributionFrequency.Monthly, _admin.Id);

        var users = new InMemoryUserRepository(new[] { _admin, _outsider });
        var stokvels = new InMemoryStokvelRepository(new[] { _stokvel });
        _contributions = new InMemoryContributionRepository();
        var idempotency = new InMemoryIdempotencyStore();

        // Built by hand: no DI container, no HTTP. This is what makes it a unit test.
        _service = new ContributionServices(
            _contributions, users, stokvels, idempotency, NullLogger<ContributionServices>.Instance);
    }

    [Fact]
    public async Task Recording_for_a_user_who_is_not_a_member_is_rejected()
    {
        var request = new RecordContributionRequest(_outsider.Id, "2026-09", 500m);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _service.RecordContributionAsync(_stokvel.Id, request, "key-1"));

        Assert.Equal("not-a-member", ex.Code);
    }

    [Fact]
    public async Task Recording_for_a_nonexistent_stokvel_is_rejected()
    {
        var request = new RecordContributionRequest(_admin.Id, "2026-09", 500m);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.RecordContributionAsync(Guid.NewGuid(), request, "key-stokvel-missing"));

        Assert.Equal("stokvel-not-found", ex.Code);
    }

    [Fact]
    public async Task Recording_for_a_nonexistent_member_is_rejected()
    {
        var request = new RecordContributionRequest(Guid.NewGuid(), "2026-09", 500m);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.RecordContributionAsync(_stokvel.Id, request, "key-member-missing"));

        Assert.Equal("member-not-found", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Recording_without_an_idempotency_key_is_rejected(string? idempotencyKey)
    {
        var request = new RecordContributionRequest(_admin.Id, "2026-09", 500m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RecordContributionAsync(_stokvel.Id, request, idempotencyKey));
    }

    [Fact]
    public async Task Recording_a_non_positive_amount_is_rejected()
    {
        var request = new RecordContributionRequest(_admin.Id, "2026-09", 0m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RecordContributionAsync(_stokvel.Id, request, "key-invalid-amount"));
    }

    [Fact]
    public async Task Recording_a_valid_contribution_succeeds_and_persists_it()
    {
        var request = new RecordContributionRequest(_admin.Id, "2026-09", 500m);

        var response = await _service.RecordContributionAsync(_stokvel.Id, request, "key-success");

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(_stokvel.Id, response.StokvelId);
        Assert.Equal(_admin.Id, response.MemberUserId);
        Assert.Equal("2026-09", response.Cycle);
        Assert.Equal(500m, response.Amount);
        Assert.NotNull(_contributions.GetById(response.Id));
    }

    [Fact]
    public async Task Recording_the_same_cycle_twice_for_the_same_member_is_rejected()
    {
        var firstRequest = new RecordContributionRequest(_admin.Id, "2026-09", 500m);
        await _service.RecordContributionAsync(_stokvel.Id, firstRequest, "key-first");

        var secondRequest = new RecordContributionRequest(_admin.Id, "2026-09", 500m);

        var ex = await Assert.ThrowsAsync<AlreadyExistsException>(
            () => _service.RecordContributionAsync(_stokvel.Id, secondRequest, "key-second"));

        Assert.Equal("contribution-already-recorded", ex.Code);
        Assert.Single(_contributions.GetAll());
    }

    [Fact]
    public async Task Duplicate_detection_ignores_surrounding_whitespace_in_the_cycle()
    {
        var firstRequest = new RecordContributionRequest(_admin.Id, "2026-09", 500m);
        await _service.RecordContributionAsync(_stokvel.Id, firstRequest, "key-whitespace-1");

        var secondRequest = new RecordContributionRequest(_admin.Id, "  2026-09  ", 500m);

        var ex = await Assert.ThrowsAsync<AlreadyExistsException>(
            () => _service.RecordContributionAsync(_stokvel.Id, secondRequest, "key-whitespace-2"));

        Assert.Equal("contribution-already-recorded", ex.Code);
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_and_payload_replays_the_original_response()
    {
        var request = new RecordContributionRequest(_admin.Id, "2026-09", 500m);

        var first = await _service.RecordContributionAsync(_stokvel.Id, request, "key-replay");
        var second = await _service.RecordContributionAsync(_stokvel.Id, request, "key-replay");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.RecordedAt, second.RecordedAt);
        Assert.Single(_contributions.GetAll());
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_with_a_different_payload_is_rejected()
    {
        var firstRequest = new RecordContributionRequest(_admin.Id, "2026-09", 500m);
        await _service.RecordContributionAsync(_stokvel.Id, firstRequest, "key-reused");

        var differentRequest = new RecordContributionRequest(_admin.Id, "2026-10", 500m);

        var ex = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(
            () => _service.RecordContributionAsync(_stokvel.Id, differentRequest, "key-reused"));

        Assert.Equal("idempotency-key-reused", ex.Code);
    }
}
