namespace TheSimontonAdventures.Web.Models;

public sealed class DestinationSection
{
    public string Heading { get; init; } = string.Empty;

    public List<string> Paragraphs { get; init; } = [];

    public string ImageSrc { get; init; } = string.Empty;

    public string ImageAlt { get; init; } = string.Empty;

    public string ImageCaption { get; init; } = string.Empty;
}