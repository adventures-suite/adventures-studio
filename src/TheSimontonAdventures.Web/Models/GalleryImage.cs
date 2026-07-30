namespace TheSimontonAdventures.Web.Models;

public sealed class GalleryImage
{
    public string Src { get; init; } = string.Empty;

    public string Alt { get; init; } = string.Empty;

    public string Caption { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}