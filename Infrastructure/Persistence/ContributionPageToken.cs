using System.Buffers.Text;
using System.Text.Json;

namespace Infrastructure.Persistence;

public sealed record ContributionPageToken(
    DateTimeOffset? LastRecordedAt,
    decimal? LastAmount,
    Guid LastId,
    string QueryFingerprint

)
{
    public string Encode() =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static ContributionPageToken? Decode(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<ContributionPageToken>(
                Base64Url.DecodeFromChars(token));
        }
        catch
        {
            return null; // malformed token → caller turns it into a 400
        }
    }
}
