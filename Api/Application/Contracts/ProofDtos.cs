namespace Api.Application.Contracts;

/// <summary>The proof attached to a contribution, without its bytes.</summary>
public sealed record ProofResponse(
    Guid ContributionId, string ContentType, string FileName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt);
