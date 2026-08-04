namespace TheSimontonAdventures.Web.Models;

public sealed class Journey
{
    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StartLocation { get; init; } = string.Empty;

    public string EndLocation { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string EndDate { get; init; } = string.Empty;

    public IReadOnlyList<JourneySegment> Segments { get; init; } =
        Array.Empty<JourneySegment>();
}