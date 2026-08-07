using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents one authored section of a destination story.</summary>
public sealed class DestinationSection
{
    /// <summary>Gets the section heading.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Gets the ordered prose paragraphs in the section.</summary>
    public List<string> Paragraphs { get; init; } = [];

    /// <summary>Gets the optional Resource Engine-resolved presentation URL.</summary>
    public string ResolvedImageUrl { get; init; } = string.Empty;

    /// <summary>Gets the optional stable Creator-owned section-image identity.</summary>
    public ResourceId? ImageResourceId { get; init; }

    /// <summary>Gets Resource Engine-authored alternative text for presentation.</summary>
    public string ResolvedImageAlternativeText { get; init; } = string.Empty;

    /// <summary>Gets the optional caption displayed with the image.</summary>
    public string ImageCaption { get; init; } = string.Empty;
}
