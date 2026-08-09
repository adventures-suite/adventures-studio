namespace TheSimontonAdventures.Web.Models;

/// <summary>Defines the map position and initial view for a destination.</summary>
public sealed class DestinationMapLocation
{
    /// <summary>Gets the latitude in decimal degrees.</summary>
    public double Latitude { get; init; }

    /// <summary>Gets the longitude in decimal degrees.</summary>
    public double Longitude { get; init; }

    /// <summary>Gets the label presented with the map marker.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the initial map zoom level.</summary>
    public int Zoom { get; init; } = 15;
}
