
using TheSimontonAdventures.Web.Models;

public interface ITravelContentService
{
    Task<Volume?> GetVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
        CancellationToken cancellationToken = default);

    Task<Volume?> GetCurrentVolumeAsync(
        CancellationToken cancellationToken = default);

    Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
        string qrSlug,
        CancellationToken cancellationToken = default);

    Task<Journey?> GetJourneyAsync(
        string volumeSlug,
        string journeySlug,
        CancellationToken cancellationToken = default);
}
