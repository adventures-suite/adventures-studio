using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Describes the media represented by a resource record.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResourceType>))]
public enum ResourceType
{
    /// <summary>An image resource.</summary>
    Image
}
