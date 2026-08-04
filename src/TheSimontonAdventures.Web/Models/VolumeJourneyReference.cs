namespace TheSimontonAdventures.Web.Models;

public sealed class VolumeJourneyReference
{
    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public JourneyType JourneyType { get; init; } = JourneyType.Unknown;

    public bool Featured { get; init; }

    public int DisplayOrder { get; init; }
}