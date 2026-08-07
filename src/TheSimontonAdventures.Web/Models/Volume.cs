using TheSimontonAdventures.Web.Resources;

namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents a book volume or major adventure collection and its referenced
/// journeys and destinations.
/// </summary>
public sealed class Volume
{
    /// <summary>Gets the volume's sequential series number.</summary>
    public int Number { get; init; }

    /// <summary>Gets the stable route identifier for the volume.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the volume's primary title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the optional supporting title.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the editorial overview of the adventure.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the root-relative book-cover image URL.</summary>
    public string CoverImage { get; init; } = string.Empty;

    /// <summary>Gets the stable Creator-owned cover resource identity.</summary>
    public ResourceId CoverResourceId { get; init; }

    /// <summary>Gets the optional wide hero image URL.</summary>
    public string HeroImage { get; init; } = string.Empty;

    /// <summary>Gets the optional stable Creator-owned wide hero resource identity.</summary>
    public ResourceId? HeroResourceId { get; init; }

    /// <summary>Gets the authored travel date range.</summary>
    public string TravelDates { get; init; } = string.Empty;

    /// <summary>Gets contextual copy explaining the current volume status.</summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>Gets the volume's editorial lifecycle state.</summary>
    public VolumeStatus Status { get; init; } = VolumeStatus.Draft;

    /// <summary>Gets concise stops used by overview timeline presentations.</summary>
    public List<JourneyStop> JourneyStops { get; init; } = [];

    /// <summary>Gets ordered references to detailed journey content.</summary>
    public List<VolumeJourneyReference> Journeys { get; init; } = [];

    /// <summary>Gets ordered references to destination content.</summary>
    public List<VolumeDestinationReference> Destinations { get; init; } = [];
}
