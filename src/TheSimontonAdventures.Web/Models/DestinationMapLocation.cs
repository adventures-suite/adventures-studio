namespace TheSimontonAdventures.Web.Models;

public sealed class DestinationMapLocation
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string Label { get; init; } = string.Empty;

    public int Zoom { get; init; } = 15;
}