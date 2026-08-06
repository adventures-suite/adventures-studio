namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Identifies journey content referenced by a volume manifest without
/// duplicating the journey itself.
/// </summary>
public sealed class VolumeJourneyReference
{
    /// <summary>Gets the referenced journey's stable identifier.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the journey title available to manifest consumers.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the referenced journey's authorship category.</summary>
    public JourneyType JourneyType { get; init; } = JourneyType.Unknown;

    /// <summary>Gets whether this journey is the volume's featured journey.</summary>
    public bool Featured { get; init; }

    /// <summary>Gets the journey's position within the volume.</summary>
    public int DisplayOrder { get; init; }
}
