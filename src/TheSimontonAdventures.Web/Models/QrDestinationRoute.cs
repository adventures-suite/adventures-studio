namespace TheSimontonAdventures.Web.Models;

public sealed class QrDestinationRoute
{
    public string QrSlug { get; init; } = string.Empty;

    public string VolumeSlug { get; init; } = string.Empty;

    public string CountrySlug { get; init; } = string.Empty;

    public string DestinationSlug { get; init; } = string.Empty;

    public string DestinationUrl =>
        $"/volumes/{VolumeSlug}/{CountrySlug}/{DestinationSlug}";
}