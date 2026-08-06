
using TheSimontonAdventures.Web.Models;

/// <summary>
/// Defines storage-independent access to volumes, destinations, journeys, and
/// stable destination addresses.
/// </summary>
public interface ITravelContentService
{
    /// <summary>Finds a volume by its stable public slug.</summary>
    /// <param name="volumeSlug">The volume slug to find.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching volume, or <see langword="null"/> when absent.</returns>
    Task<Volume?> GetVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a destination using its complete canonical route identity.</summary>
    /// <param name="volumeSlug">The owning volume slug.</param>
    /// <param name="countrySlug">The country route segment.</param>
    /// <param name="destinationSlug">The destination route segment.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching destination, or <see langword="null"/> when absent.</returns>
    Task<Destination?> GetDestinationAsync(
        string volumeSlug,
        string countrySlug,
        string destinationSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available destination content in the display order declared
    /// by the specified volume manifest.
    /// </summary>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The destinations whose referenced content can be loaded.</returns>
    Task<IReadOnlyList<Destination>> GetDestinationsForVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves published destinations for the specified volume in manifest
    /// display order.
    /// </summary>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The volume's available, published destinations.</returns>
    Task<IReadOnlyList<Destination>> GetPublishedDestinationsForVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves published, featured destinations for the specified volume in
    /// homepage presentation order.
    /// </summary>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The volume's published destinations selected for featuring.</returns>
    Task<IReadOnlyList<Destination>> GetFeaturedDestinationsAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves every available volume in series order.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>All volume manifests that can be loaded successfully.</returns>
    Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves volumes whose lifecycle state permits public display.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Publicly visible volumes in series order.</returns>
    Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Finds the first volume designated as the current adventure.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The current volume, or <see langword="null"/> when none is designated.</returns>
    Task<Volume?> GetCurrentVolumeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Finds a published destination route by its stable QR slug.</summary>
    /// <param name="qrSlug">The stable public slug to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Resolved route identity, or <see langword="null"/> when unavailable.</returns>
    Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
        string qrSlug,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a journey within a volume by its stable slug.</summary>
    /// <param name="volumeSlug">The owning volume slug.</param>
    /// <param name="journeySlug">The journey slug to find.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching journey, or <see langword="null"/> when absent.</returns>
    Task<Journey?> GetJourneyAsync(
        string volumeSlug,
        string journeySlug,
        CancellationToken cancellationToken = default);
}
