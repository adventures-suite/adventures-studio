namespace TheSimontonAdventures.Web.Models;

public sealed class JourneySegment
{
    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;

    public GeoCoordinate? StartCoordinate { get; init; }

    public GeoCoordinate? EndCoordinate { get; init; }

    public IReadOnlyList<JourneyWaypoint> Waypoints { get; init; } =
        Array.Empty<JourneyWaypoint>();

    public TravelMode TravelMode { get; init; } = TravelMode.Unknown;

    public string TravelDescription { get; init; } = string.Empty;

    public string DepartureDate { get; init; } = string.Empty;

    public string ArrivalDate { get; init; } = string.Empty;

    public string CountrySlug { get; init; } = string.Empty;

    public string DestinationSlug { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}