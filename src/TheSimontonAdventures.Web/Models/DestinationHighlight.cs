namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents a notable attraction or experience at a destination.</summary>
public sealed class DestinationHighlight
{
    /// <summary>Gets the highlight title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the editorial explanation of why the highlight matters.</summary>
    public string Description { get; init; } = string.Empty;
}
