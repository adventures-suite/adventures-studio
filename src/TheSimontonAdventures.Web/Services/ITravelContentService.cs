using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Services;

public interface ITravelContentService
{
    Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CancellationToken cancellationToken = default);

    Task<Volume?> GetVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default);

    Task<Destination?> GetDestinationAsync(
        string volumeSlug,
        string countrySlug,
        string destinationSlug,
        CancellationToken cancellationToken = default);
}