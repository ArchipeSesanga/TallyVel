using System.Globalization;
using Domain;

namespace Api.Common;

/// <summary>
/// Reads a UTC instant from a query string. Only ISO 8601 that ENDS IN Z is accepted
/// ("2026-10-31T22:00:00Z", optionally with fractional seconds). An offset ("+02:00"), a bare
/// date or a local time is refused, because the API would have to guess which time zone was meant.
/// </summary>
public static class UtcInstant
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
    ];

    /// <summary>Null when the parameter was not sent; otherwise the instant, or a 400 (InvalidQueryException).</summary>
    public static DateTimeOffset? Parse(string? text, string parameter)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateTimeOffset.TryParseExact(
                text.Trim(), Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant))
            return instant;

        throw new InvalidQueryException(parameter,
            $"'{parameter}' must be a UTC time in ISO 8601 ending in Z, for example 2026-10-31T22:00:00Z.");
    }
}
