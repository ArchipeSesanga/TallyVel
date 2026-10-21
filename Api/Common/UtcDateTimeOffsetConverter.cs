using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Common;

/// <summary>
/// Writes every DateTimeOffset as a UTC instant ending in "Z" ("2026-10-31T22:30:00Z").
/// System.Text.Json would write "+00:00" for the same instant; a "Z" is what
/// the frontend and every date library expect, and it never shifts by zone.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    // UtcDateTime has Kind=Utc, which Utf8JsonWriter writes with a trailing Z.
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime);
}
