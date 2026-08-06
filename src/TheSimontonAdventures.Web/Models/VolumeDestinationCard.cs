namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents destination content projected for display in a volume card grid.
/// </summary>
public sealed class VolumeDestinationCard
{
    /// <summary>Gets the country route segment.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the destination route segment.</summary>
    public string DestinationSlug { get; init; } = string.Empty;

    /// <summary>Gets the destination's display title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the human-readable country name.</summary>
    public string CountryName { get; init; } = string.Empty;

    /// <summary>Gets the selected card image URL.</summary>
    public string HeroImage { get; init; } = string.Empty;

    /// <summary>Gets the selected card summary.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets the card's position in the volume grid.</summary>
    public int DisplayOrder { get; init; }
}
