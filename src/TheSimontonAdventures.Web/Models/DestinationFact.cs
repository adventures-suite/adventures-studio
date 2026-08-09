namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents a labeled fact displayed in a destination guide.</summary>
public sealed class DestinationFact
{
    /// <summary>Gets the short descriptive label for the fact.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the human-readable fact value.</summary>
    public string Value { get; init; } = string.Empty;
}
