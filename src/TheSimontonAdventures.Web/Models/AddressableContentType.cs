namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Identifies the platform content category represented by a stable public address.
/// </summary>
/// <remarks>
/// This enum is intentionally broader than the content types currently implemented.
///
/// The Address Engine uses this value to describe what kind of platform object
/// a public slug resolves to without coupling address resolution to a specific
/// Razor page, storage model, or rendering implementation.
///
/// New values may be added as AdventuresSuite introduces additional addressable
/// content types.
/// </remarks>
public enum AddressableContentType
{
    /// <summary>
    /// Indicates that the content type is unknown, unavailable, or has not yet
    /// been classified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Represents a published book volume or major adventure collection.
    /// </summary>
    Volume,

    /// <summary>
    /// Represents a complete journey or adventure.
    /// </summary>
    Journey,

    /// <summary>
    /// Represents an individual segment within a journey.
    /// </summary>
    JourneySegment,

    /// <summary>
    /// Represents a destination, city, region, or major place.
    /// </summary>
    Destination,

    /// <summary>
    /// Represents a specific attraction, activity, tour, landmark, or experience
    /// associated with a destination or journey.
    /// </summary>
    Experience,

    /// <summary>
    /// Represents a complete authored story.
    /// </summary>
    Story,

    /// <summary>
    /// Represents an addressable section within a larger story.
    /// </summary>
    StorySection,

    /// <summary>
    /// Represents a personal reflection associated with an adventure or content item.
    /// </summary>
    Reflection,

    /// <summary>
    /// Represents an addressable quotation or pull quote.
    /// </summary>
    Quote,

    /// <summary>
    /// Represents a collection of photographs or other visual media.
    /// </summary>
    Gallery,

    /// <summary>
    /// Represents an individual photograph.
    /// </summary>
    Photograph,

    /// <summary>
    /// Represents authored or published video content.
    /// </summary>
    Video,

    /// <summary>
    /// Represents authored or published audio content.
    /// </summary>
    Audio,

    /// <summary>
    /// Represents an interactive or static map.
    /// </summary>
    Map,

    /// <summary>
    /// Represents a guide, reference, or planning-oriented content item.
    /// </summary>
    Guide,

    /// <summary>
    /// Represents a reusable supporting resource such as a document, link,
    /// reservation, logo, downloadable file, or media asset.
    /// </summary>
    Resource,

    /// <summary>
    /// Represents an approved address that resolves outside AdventuresSuite.
    /// </summary>
    ExternalUrl,

    /// <summary>
    /// Represents an addressable content type that does not fit another
    /// currently defined category.
    /// </summary>
    Other
}