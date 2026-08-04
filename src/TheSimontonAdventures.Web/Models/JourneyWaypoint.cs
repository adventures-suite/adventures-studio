namespace TheSimontonAdventures.Web.Models;

public sealed class JourneyWaypoint
{
    public string Title { get; init; } = string.Empty;

    public GeoCoordinate Coordinate { get; init; } = new();

    public string Notes { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}