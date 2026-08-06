namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents an ordered journey and the travel segments that connect its
/// locations.
/// </summary>
public sealed class Journey
{
    /// <summary>Gets the journey's stable identifier within its volume.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the slug of the volume that owns the journey.</summary>
    public string VolumeSlug { get; init; } = string.Empty;

    /// <summary>Gets the journey's public title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the editorial overview of the complete journey.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets the journey's ownership and authorship category.</summary>
    public JourneyType JourneyType { get; init; } = JourneyType.Unknown;

    /// <summary>Gets the human-readable starting location.</summary>
    public string StartLocation { get; init; } = string.Empty;

    /// <summary>Gets the human-readable ending location.</summary>
    public string EndLocation { get; init; } = string.Empty;

    /// <summary>Gets the authored start date text.</summary>
    public string StartDate { get; init; } = string.Empty;

    /// <summary>Gets the authored end date text.</summary>
    public string EndDate { get; init; } = string.Empty;

    /// <summary>Gets whether the journey is available to public consumers.</summary>
    public bool Published { get; init; }

    /// <summary>Gets the journey's position within its volume.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>Gets the ordered travel segments that make up the journey.</summary>
    public IReadOnlyList<JourneySegment> Segments { get; init; } =
        Array.Empty<JourneySegment>();
}
