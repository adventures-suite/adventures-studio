
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Defines Creator-scoped, storage-independent access to volumes,
/// destinations, journeys, and stable destination addresses.
/// </summary>
public interface ITravelContentService
{
    /// <summary>Gets Creator-authored About-page content.</summary>
    /// <param name="creatorId">The owning Creator identity.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The Creator profile, or <see langword="null"/> when absent.</returns>
    Task<CreatorProfile?> GetCreatorProfileAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a volume by its stable public slug.</summary>
    /// <param name="creatorId">The Creator that owns the volume.</param>
    /// <param name="volumeSlug">The volume slug to find.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching volume, or <see langword="null"/> when absent.</returns>
    Task<Volume?> GetVolumeAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a destination using its complete canonical route identity.</summary>
    /// <param name="creatorId">The Creator that owns the destination.</param>
    /// <param name="volumeSlug">The owning volume slug.</param>
    /// <param name="countrySlug">The country route segment.</param>
    /// <param name="destinationSlug">The destination route segment.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching destination, or <see langword="null"/> when absent.</returns>
    Task<Destination?> GetDestinationAsync(
        CreatorId creatorId,
        string volumeSlug,
        string countrySlug,
        string destinationSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available destination content in the display order declared
    /// by the specified volume manifest.
    /// </summary>
    /// <param name="creatorId">The Creator that owns the volume.</param>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The destinations whose referenced content can be loaded.</returns>
    Task<IReadOnlyList<Destination>> GetDestinationsForVolumeAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves published destinations for the specified volume in manifest
    /// display order.
    /// </summary>
    /// <param name="creatorId">The Creator that owns the volume.</param>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The volume's available, published destinations.</returns>
    Task<IReadOnlyList<Destination>> GetPublishedDestinationsForVolumeAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves published, featured destinations for the specified volume in
    /// homepage presentation order.
    /// </summary>
    /// <param name="creatorId">The Creator that owns the volume.</param>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The volume's published destinations selected for featuring.</returns>
    Task<IReadOnlyList<Destination>> GetFeaturedDestinationsAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves every available volume in series order.</summary>
    /// <param name="creatorId">The Creator whose volumes should be returned.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>All volume manifests that can be loaded successfully.</returns>
    Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves volumes whose lifecycle state permits public display.</summary>
    /// <param name="creatorId">The Creator whose public volumes should be returned.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Publicly visible volumes in series order.</returns>
    Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds the first volume designated as the current adventure.</summary>
    /// <param name="creatorId">The Creator whose current volume should be returned.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The current volume, or <see langword="null"/> when none is designated.</returns>
    Task<Volume?> GetCurrentVolumeAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a published destination route by its stable QR slug.</summary>
    /// <param name="creatorId">The Creator that owns the public address.</param>
    /// <param name="qrSlug">The stable public slug to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Resolved route identity, or <see langword="null"/> when unavailable.</returns>
    Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
        CreatorId creatorId,
        string qrSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a journey within a volume by its stable slug.</summary>
    /// <param name="creatorId">The Creator that owns the journey.</param>
    /// <param name="volumeSlug">The owning volume slug.</param>
    /// <param name="journeySlug">The journey slug to find.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching journey, or <see langword="null"/> when absent.</returns>
    Task<Journey?> GetJourneyAsync(
        CreatorId creatorId,
        string volumeSlug,
        string journeySlug,
        CancellationToken cancellationToken = default);
}
