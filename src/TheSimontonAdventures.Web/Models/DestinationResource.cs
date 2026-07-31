namespace TheSimontonAdventures.Web.Models;

public sealed class DestinationResource
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public int DisplayOrder { get; init; }
}