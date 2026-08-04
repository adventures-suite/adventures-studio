namespace TheSimontonAdventures.Web.Models;

public sealed class Volume
{
    public int Number { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string CoverImage { get; init; } = string.Empty;

    public string HeroImage { get; init; } = string.Empty;

    public string TravelDates { get; init; } = string.Empty;

    public string StatusMessage { get; init; } = string.Empty;

    public VolumeStatus Status { get; init; } = VolumeStatus.Draft;

    public List<JourneyStop> JourneyStops { get; init; } = [];

    public List<VolumeJourneyReference> Journeys { get; init; } = [];

    public List<VolumeDestinationReference> Destinations { get; init; } = [];
}