namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents one movement between two locations in a journey.</summary>
public sealed class JourneySegment
{
    /// <summary>Gets the human-readable departure location.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Gets the human-readable arrival location.</summary>
    public string To { get; init; } = string.Empty;

    /// <summary>Gets the optional geographic departure point.</summary>
    public GeoCoordinate? StartCoordinate { get; init; }

    /// <summary>Gets the optional geographic arrival point.</summary>
    public GeoCoordinate? EndCoordinate { get; init; }

    /// <summary>Gets ordered intermediate locations along the segment.</summary>
    public IReadOnlyList<JourneyWaypoint> Waypoints { get; init; } =
        Array.Empty<JourneyWaypoint>();

    /// <summary>Gets the primary mode of travel for the segment.</summary>
    public TravelMode TravelMode { get; init; } = TravelMode.Unknown;

    /// <summary>Gets editorial detail about the transportation used.</summary>
    public string TravelDescription { get; init; } = string.Empty;

    /// <summary>Gets the authored departure date or time text.</summary>
    public string DepartureDate { get; init; } = string.Empty;

    /// <summary>Gets the authored arrival date or time text.</summary>
    public string ArrivalDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the typed local visit schedule for the destination reached by this
    /// segment.
    /// </summary>
    public JourneyVisitSchedule? VisitSchedule { get; init; }

    /// <summary>Gets the country route segment for a linked destination.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the destination route segment when the arrival is linked.</summary>
    public string DestinationSlug { get; init; } = string.Empty;

    /// <summary>Gets optional editorial notes about the segment.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Gets the segment's position within the journey.</summary>
    public int DisplayOrder { get; init; }
}
