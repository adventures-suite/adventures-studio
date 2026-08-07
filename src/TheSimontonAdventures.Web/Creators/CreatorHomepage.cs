namespace TheSimontonAdventures.Web.Creators;

/// <summary>
/// Defines which shared sections a Creator places on its homepage and the
/// order in which they are rendered.
/// </summary>
public sealed class CreatorHomepage
{
    /// <summary>Gets the ordered shared homepage section identifiers.</summary>
    public IReadOnlyList<CreatorHomepageSectionType> Sections { get; init; } = [];
}
