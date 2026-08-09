namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents practical advice for visiting a destination.</summary>
public sealed class DestinationTip
{
    /// <summary>Gets the concise tip title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the supporting recommendation or explanation.</summary>
    public string Description { get; init; } = string.Empty;
}
