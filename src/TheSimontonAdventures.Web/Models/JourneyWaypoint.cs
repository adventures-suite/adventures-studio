namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents an intermediate location within a journey segment.</summary>
public sealed class JourneyWaypoint
{
    /// <summary>Gets the waypoint's human-readable title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the waypoint's geographic coordinate.</summary>
    public GeoCoordinate Coordinate { get; init; } = new();

    /// <summary>Gets optional editorial notes about the waypoint.</summary>
    public string Notes { get; init; } = string.Empty;

    /// <summary>Gets the waypoint's position within its segment.</summary>
    public int DisplayOrder { get; init; }
}
