namespace TallyVel.Api.Application.Contracts;

/// <summary>
/// One page of a list endpoint. NextPageToken is opaque to the client:
/// pass it back as ?pageToken= to get the next page. An empty string
/// means this is the last page. No total count, on purpose — counting
/// would cost an extra COUNT(*) query on every page.
/// </summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string NextPageToken);
