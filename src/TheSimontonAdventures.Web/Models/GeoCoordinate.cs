namespace TheSimontonAdventures.Web.Models;

public sealed class GeoCoordinate
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string Label { get; init; } = string.Empty;
}