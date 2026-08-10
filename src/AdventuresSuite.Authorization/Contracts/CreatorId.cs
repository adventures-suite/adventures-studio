using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Identifies a Creator independently of mutable names, slugs, domains, and
/// storage locations.
/// </summary>
[JsonConverter(typeof(CreatorIdJsonConverter))]
public readonly record struct CreatorId
{
    /// <summary>
    /// Initializes a stable Creator identity from its canonical value.
    /// </summary>
    /// <param name="value">The canonical, storage-independent identity.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty, contains surrounding whitespace, or
    /// contains characters outside lowercase ASCII letters, digits, and
    /// underscores.
    /// </exception>
    public CreatorId(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "Creator identity must contain 3-64 lowercase letters, digits, " +
                "or underscores and must begin with a letter.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical Creator identity value.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the canonical identity value.
    /// </summary>
    /// <returns>The canonical identity value.</returns>
    public override string ToString() => Value ?? string.Empty;

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 64
            || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '_');
    }

    private sealed class CreatorIdJsonConverter : JsonConverter<CreatorId>
    {
        public override CreatorId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Creator identity must be a JSON string.");
            }

            try
            {
                return new CreatorId(reader.GetString()!);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException("Creator identity is invalid.", exception);
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            CreatorId value,
            JsonSerializerOptions options)
        {
            if (value == default)
            {
                throw new JsonException("The default Creator identity is invalid.");
            }

            writer.WriteStringValue(value.Value);
        }
    }
}
