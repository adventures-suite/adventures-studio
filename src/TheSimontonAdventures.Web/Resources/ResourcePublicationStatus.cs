using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Resources;

/// <summary>Controls whether a resource may be exposed by public content.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResourcePublicationStatus>))]
public enum ResourcePublicationStatus
{
    /// <summary>The resource is not publicly addressable.</summary>
    Draft,

    /// <summary>The resource may be used by public content.</summary>
    Published
}
