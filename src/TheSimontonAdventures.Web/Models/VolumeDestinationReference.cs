namespace TheSimontonAdventures.Web.Models;

public sealed class VolumeDestinationReference
{
    public string CountrySlug { get; init; } = string.Empty;

    public string DestinationSlug { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}