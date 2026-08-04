namespace TheSimontonAdventures.Web.Models;

public sealed class Journey
{
    public string Slug { get; init; } = string.Empty;

    public string VolumeSlug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public JourneyType JourneyType { get; init; } = JourneyType.Unknown;

    public string StartLocation { get; init; } = string.Empty;

    public string EndLocation { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string EndDate { get; init; } = string.Empty;

    public bool Published { get; init; }

    public int DisplayOrder { get; init; }

    public IReadOnlyList<JourneySegment> Segments { get; init; } =
        Array.Empty<JourneySegment>();
}