using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Identifies a reusable resource independently of its storage location.</summary>
[JsonConverter(typeof(ResourceIdJsonConverter))]
public readonly record struct ResourceId
{
    /// <summary>Initializes a stable resource identity.</summary>
    /// <param name="value">The canonical resource identity.</param>
    public ResourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length is < 3 or > 96
            || value[0] is < 'a' or > 'z'
            || !value.All(character => character is >= 'a' and <= 'z'
                or >= '0' and <= '9' or '_'))
        {
            throw new ArgumentException(
                "Resource identity must contain 3-96 lowercase letters, digits, or underscores and begin with a letter.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical resource identity.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;

    private sealed class ResourceIdJsonConverter : JsonConverter<ResourceId>
    {
        public override ResourceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Resource identity must be a JSON string.");
            }

            try
            {
                return new ResourceId(reader.GetString()!);
            }
            catch (ArgumentException exception)
            {
                throw new JsonException("Resource identity is invalid.", exception);
            }
        }

        public override void Write(Utf8JsonWriter writer, ResourceId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
