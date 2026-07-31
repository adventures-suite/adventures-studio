namespace TheSimontonAdventures.Web.Models;

public sealed class Destination
{
    public string VolumeSlug { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public string CountrySlug { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string HeroImage { get; init; } = string.Empty;

    public string HeroImageAlt { get; init; } = string.Empty;

    public bool Published { get; init; }

    public List<DestinationSection> Sections { get; init; } = [];

    public List<DestinationFact> Facts { get; init; } = [];

    public List<DestinationHighlight> Highlights { get; init; } = [];

    public List<DestinationTip> Tips { get; init; } = [];

    public List<GalleryImage> Gallery { get; init; } = [];

    public DestinationMapLocation? Map { get; init; }

    public List<DestinationResource> Resources { get; init; } = [];
}