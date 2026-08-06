namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents an image displayed in a destination gallery.</summary>
public sealed class GalleryImage
{
    /// <summary>Gets the root-relative or absolute image URL.</summary>
    public string Src { get; init; } = string.Empty;

    /// <summary>Gets accessible alternative text for the image.</summary>
    public string Alt { get; init; } = string.Empty;

    /// <summary>Gets the optional editorial caption.</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>Gets the image's position within the gallery.</summary>
    public int DisplayOrder { get; init; }
}
