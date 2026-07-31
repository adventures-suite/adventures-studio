namespace TheSimontonAdventures.Web.Models;

public sealed class JourneyStop
{
    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string CountrySlug { get; init; } = string.Empty;

    public string DestinationSlug { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}