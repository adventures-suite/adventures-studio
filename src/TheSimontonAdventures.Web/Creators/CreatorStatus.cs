using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Creators;

/// <summary>Describes whether a Creator may expose public platform content.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CreatorStatus
{
    /// <summary>The Creator is being configured and is not publicly available.</summary>
    Draft,

    /// <summary>The Creator is active and may expose published content.</summary>
    Active,

    /// <summary>The Creator is temporarily inactive and exposes no public content.</summary>
    Inactive,

    /// <summary>The Creator has been administratively disabled.</summary>
    Disabled
}
