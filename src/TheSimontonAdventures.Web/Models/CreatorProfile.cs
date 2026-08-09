using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents Creator-authored editorial content for the About page.</summary>
public sealed class CreatorProfile
{
    /// <summary>Gets the primary About-page heading.</summary>
    public string Headline { get; init; } = string.Empty;

    /// <summary>Gets the introductory lead paragraph.</summary>
    public string Lead { get; init; } = string.Empty;

    /// <summary>Gets the heading for the detailed Creator story.</summary>
    public string StoryTitle { get; init; } = string.Empty;

    /// <summary>Gets the paragraphs shown before the highlighted statement.</summary>
    public IReadOnlyList<string> IntroductionParagraphs { get; init; } = [];

    /// <summary>Gets the optional highlighted statement.</summary>
    public string Highlight { get; init; } = string.Empty;

    /// <summary>Gets the paragraphs shown after the highlighted statement.</summary>
    public IReadOnlyList<string> StoryParagraphs { get; init; } = [];

    /// <summary>Gets the root-relative or absolute About hero image URL.</summary>
    /// <summary>Gets the optional stable Creator-owned About hero resource identity.</summary>
    public ResourceId? HeroResourceId { get; init; }
}
