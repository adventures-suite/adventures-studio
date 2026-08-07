using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Preserves ISO 8601 input offsets for validation and writes valid UTC values
/// using the canonical trailing-<c>Z</c> JSON representation.
/// </summary>
internal sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    /// <inheritdoc />
    public override DateTimeOffset? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String
            || !DateTimeOffset.TryParse(
                reader.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var value))
        {
            throw new JsonException(
                "Lifecycle timestamps must use a valid ISO 8601 representation.");
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
            CultureInfo.InvariantCulture));
    }
}
