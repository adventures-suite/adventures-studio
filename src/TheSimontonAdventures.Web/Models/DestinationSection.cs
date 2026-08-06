namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents one authored section of a destination story.</summary>
public sealed class DestinationSection
{
    /// <summary>Gets the section heading.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Gets the ordered prose paragraphs in the section.</summary>
    public List<string> Paragraphs { get; init; } = [];

    /// <summary>Gets the optional root-relative supporting image URL.</summary>
    public string ImageSrc { get; init; } = string.Empty;

    /// <summary>Gets accessible alternative text for the supporting image.</summary>
    public string ImageAlt { get; init; } = string.Empty;

    /// <summary>Gets the optional caption displayed with the image.</summary>
    public string ImageCaption { get; init; } = string.Empty;
}
