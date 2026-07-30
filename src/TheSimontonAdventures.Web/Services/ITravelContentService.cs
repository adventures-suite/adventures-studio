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
}