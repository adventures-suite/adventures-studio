namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents a concise itinerary stop used by overview timeline components.
/// </summary>
public sealed class JourneyStop
{
    /// <summary>Gets the stop's human-readable title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets supporting context displayed beneath the title.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the country route segment when the stop is linked.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the destination route segment when the stop is linked.</summary>
    public string DestinationSlug { get; init; } = string.Empty;

    /// <summary>Gets the compact visual symbol used by legacy timelines.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Gets the serialized travel-mode name used by timeline icons.</summary>
    public string TravelMode { get; init; } = string.Empty;

    /// <summary>Gets supporting transportation detail for the stop.</summary>
    public string TravelDescription { get; init; } = string.Empty;

    /// <summary>Gets the stop's position in the itinerary.</summary>
    public int DisplayOrder { get; init; }
}
