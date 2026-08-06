using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

/// <summary>Identifies how a journey is authored and intended to be used.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JourneyType>))]
public enum JourneyType
{
    /// <summary>Indicates that the journey category is not known.</summary>
    Unknown = 0,

    /// <summary>Represents a journey authored as published editorial content.</summary>
    Editorial = 1,

    /// <summary>Represents a suggested journey assembled for discovery.</summary>
    Recommended = 2,

    /// <summary>Represents a journey created or customized by a customer.</summary>
    Customer = 3,

    /// <summary>Represents a reusable journey structure used as a starting point.</summary>
    Template = 4
}
