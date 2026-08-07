using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents an image displayed in a destination gallery.</summary>
public sealed class GalleryImage
{
    /// <summary>Gets the stable Creator-owned gallery resource identity.</summary>
    public ResourceId ResourceId { get; init; }

    /// <summary>Gets the Resource Engine-resolved public URL used by presentation.</summary>
    public string PublicUrl { get; init; } = string.Empty;

    /// <summary>Gets Resource Engine-authored alternative text used by presentation.</summary>
    public string AlternativeText { get; init; } = string.Empty;

    /// <summary>Gets the Resource Engine-authored public attribution.</summary>
    public string Attribution { get; init; } = string.Empty;

    /// <summary>Gets the Resource Engine-authored copyright notice.</summary>
    public string Copyright { get; init; } = string.Empty;

    /// <summary>Gets the optional editorial caption.</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>Gets the image's position within the gallery.</summary>
    public int DisplayOrder { get; init; }
}
