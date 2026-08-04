
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