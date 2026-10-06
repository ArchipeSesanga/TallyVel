using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
namespace TallyVel.Api.Application;

public sealed record ContributionPageToken(
    DateTimeOffset? LastRecordedAt,
    decimal? LastAmount,
    Guid LastId,
    string QueryFingerprint

)
{
    public string Encode() =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(this));

    public static ContributionPageToken? Decode(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<ContributionPageToken>(
                WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            return null; // malformed token → caller turns it into a 400
        }
    }
}