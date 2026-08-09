using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>Identifies a shared section available for Creator homepage composition.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CreatorHomepageSectionType>))]
public enum CreatorHomepageSectionType
{
    /// <summary>Displays the Creator's current public Adventure.</summary>
    CurrentAdventure = 1,

    /// <summary>Displays the Creator's upcoming and planned Adventures.</summary>
    PlannedAdventures = 2,

    /// <summary>Displays published destinations selected for homepage featuring.</summary>
    FeaturedDestinations = 3
}
