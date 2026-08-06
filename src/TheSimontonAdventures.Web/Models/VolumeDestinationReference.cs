namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Identifies destination content referenced by a volume manifest without
/// duplicating the destination itself.
/// </summary>
public sealed class VolumeDestinationReference
{
    /// <summary>Gets the referenced destination's country route segment.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the referenced destination's route segment.</summary>
    public string DestinationSlug { get; init; } = string.Empty;

    /// <summary>Gets the destination's position within the volume.</summary>
    public int DisplayOrder { get; init; }
}
