namespace TheSimontonAdventures.Web.Models;

public sealed class DestinationSection
{
    public string Heading { get; init; } = string.Empty;

    public List<string> Paragraphs { get; init; } = [];
}