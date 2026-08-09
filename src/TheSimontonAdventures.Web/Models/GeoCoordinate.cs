namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents a labeled geographic coordinate.</summary>
public sealed class GeoCoordinate
{
    /// <summary>Gets the latitude in decimal degrees.</summary>
    public double Latitude { get; init; }

    /// <summary>Gets the longitude in decimal degrees.</summary>
    public double Longitude { get; init; }

    /// <summary>Gets the optional human-readable location label.</summary>
    public string Label { get; init; } = string.Empty;
}
