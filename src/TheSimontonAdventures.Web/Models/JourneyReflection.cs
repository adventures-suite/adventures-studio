namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents a personal reflection embedded in a destination story.</summary>
public sealed class JourneyReflection
{
    /// <summary>Gets the one-based story section after which this appears.</summary>
    public int AfterSection { get; init; }

    /// <summary>Gets the reflection author's display name.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Gets the optional reflection title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the authored reflection text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the optional signature or closing attribution.</summary>
    public string Signature { get; init; } = string.Empty;
}
