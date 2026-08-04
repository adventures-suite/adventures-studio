namespace TheSimontonAdventures.Web.Models;

public sealed class VolumeDestinationCard
{
    public string CountrySlug { get; init; } = string.Empty;

    public string DestinationSlug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string CountryName { get; init; } = string.Empty;

    public string HeroImage { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}