namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents an external resource related to a destination.</summary>
public sealed class DestinationResource
{
    /// <summary>Gets the resource's display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a brief explanation of the resource.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the internal or external URL opened by the resource.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Gets the editorial category used to group the resource.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Gets the resource's position within its destination listing.</summary>
    public int DisplayOrder { get; init; }
}
