namespace TheSimontonAdventures.Web.Models;

/// <summary>Represents a quotation associated with a destination story.</summary>
public sealed class DestinationQuote
{
    /// <summary>Gets the quoted text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the person or source credited for the quotation.</summary>
    public string Attribution { get; init; } = string.Empty;
}
