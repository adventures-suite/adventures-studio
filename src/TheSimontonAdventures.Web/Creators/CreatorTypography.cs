using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Identifies an approved shared typography treatment without allowing
/// arbitrary Creator-supplied CSS.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CreatorTypography
{
    /// <summary>Uses the platform's traditional editorial type treatment.</summary>
    Classic,

    /// <summary>Uses a contemporary system sans-serif type treatment.</summary>
    Modern
}
