using TheSimontonAdventures.Web.Resources;
using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents the complete authored content and publication metadata for a
/// destination page.
/// </summary>
public sealed class Destination
{
    /// <summary>Gets the slug of the volume that owns this destination.</summary>
    public string VolumeSlug { get; init; } = string.Empty;

    /// <summary>Gets the destination's human-readable country name.</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>Gets the country segment used in the canonical route.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the city or locality associated with the destination.</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>Gets the destination segment used in the canonical route.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the stable public slug encoded by printed QR codes.</summary>
    public string QrSlug { get; init; } = string.Empty;

    /// <summary>Gets historical public slugs that resolve to this destination.</summary>
    public IReadOnlyList<string> QrAliases { get; init; } = [];

    /// <summary>Gets the primary editorial title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the optional supporting title displayed with the hero.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the general-purpose editorial summary.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets the root-relative hero image URL.</summary>
    /// <summary>Gets the stable Creator-owned hero resource identity.</summary>
    public ResourceId HeroResourceId { get; init; }

    /// <summary>Gets accessible alternative text for the hero image.</summary>
    /// <summary>Gets whether the destination is available to public consumers.</summary>
    public bool Published { get; init; }

    /// <summary>Gets whether the destination is eligible for featured listings.</summary>
    public bool Featured { get; init; }

    /// <summary>Gets the destination's position in homepage featured content.</summary>
    public int HomepageOrder { get; init; }

    /// <summary>Gets the optional image optimized for cards and homepage use.</summary>
    /// <summary>Gets the stable Creator-owned card-image resource identity.</summary>
    public ResourceId HomepageResourceId { get; init; }

    /// <summary>Gets the optional summary optimized for cards and homepage use.</summary>
    public string HomepageSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional IANA time-zone identifier for the destination.
    /// </summary>
    [JsonPropertyName("timeZone")]
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Gets the expected first date at the destination.</summary>
    [JsonPropertyName("plannedArrivalDate")]
    public DateOnly? PlannedArrivalDate { get; init; }

    /// <summary>Gets the expected final date at the destination.</summary>
    [JsonPropertyName("plannedDepartureDate")]
    public DateOnly? PlannedDepartureDate { get; init; }

    /// <summary>Gets the first date actually spent at the destination.</summary>
    [JsonPropertyName("visitedFrom")]
    public DateOnly? VisitedFrom { get; init; }

    /// <summary>Gets the final date actually spent at the destination.</summary>
    [JsonPropertyName("visitedTo")]
    public DateOnly? VisitedTo { get; init; }

    /// <summary>Gets the UTC timestamp when this content record was authored.</summary>
    [JsonPropertyName("createdAtUtc")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public DateTimeOffset? CreatedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp of the latest meaningful authored change.</summary>
    [JsonPropertyName("updatedAtUtc")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public DateTimeOffset? UpdatedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp of the destination's first publication.</summary>
    [JsonPropertyName("publishedAtUtc")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public DateTimeOffset? PublishedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp of the latest meaningful publication.</summary>
    [JsonPropertyName("lastPublishedAtUtc")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public DateTimeOffset? LastPublishedAtUtc { get; init; }

    /// <summary>Gets the ordered editorial story sections.</summary>
    public List<DestinationSection> Sections { get; init; } = [];

    /// <summary>Gets personal reflections placed within the destination story.</summary>
    public List<JourneyReflection> Reflections { get; init; } = [];

    /// <summary>Gets the optional pull quote associated with the destination.</summary>
    public DestinationQuote? Quote { get; init; }

    /// <summary>Gets concise factual details shown in the destination guide.</summary>
    public List<DestinationFact> Facts { get; init; } = [];

    /// <summary>Gets notable places or experiences highlighted by the guide.</summary>
    public List<DestinationHighlight> Highlights { get; init; } = [];

    /// <summary>Gets practical travel advice associated with the destination.</summary>
    public List<DestinationTip> Tips { get; init; } = [];

    /// <summary>Gets the ordered photography collection.</summary>
    public List<GalleryImage> Gallery { get; init; } = [];

    /// <summary>Gets optional map coordinates and display settings.</summary>
    public DestinationMapLocation? Map { get; init; }

    /// <summary>Gets ordered external resources for continuing the journey.</summary>
    public List<DestinationResource> Resources { get; init; } = [];
}
