using Api.Application.Contracts;

namespace Api.Application.Interfaces;
public interface IContributionService
{
     Task<ContributionResponse> RecordContributionAsync(
        Guid stokvelId,
        RecordContributionRequest request,
        string? idempotencyKey,
        CancellationToken ct = default);
}