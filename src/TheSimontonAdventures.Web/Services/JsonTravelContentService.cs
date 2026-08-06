using System.Text.Json;
using TheSimontonAdventures.Web.Models;
using System.Text.Json.Serialization;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Loads strongly typed travel content from version-controlled JSON files
/// beneath the application's content root.
/// </summary>
public sealed class JsonTravelContentService : ITravelContentService
{
    private readonly string _volumesDirectory;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>Initializes the service using the host's deployed content root.</summary>
    /// <param name="hostEnvironment">The active application host environment.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="hostEnvironment"/> is <see langword="null"/>.
    /// </exception>
    public JsonTravelContentService(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        _volumesDirectory = Path.Combine(
            hostEnvironment.ContentRootPath,
            "Content",
            "Volumes");

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_volumesDirectory))
        {
            return [];
        }

        var manifestPaths = Directory
            .EnumerateFiles(
                _volumesDirectory,
                "volume.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        var volumes = new List<Volume>();

        foreach (var manifestPath in manifestPaths)
        {
            var volume = await DeserializeFileAsync<Volume>(
                manifestPath,
                cancellationToken);

            if (volume is not null)
            {
                volumes.Add(volume);
            }
        }

        return volumes
            .OrderBy(volume => volume.Number)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Volume?> GetVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeSlug);

        var volumes = await GetVolumesAsync(cancellationToken);

        return volumes.FirstOrDefault(
            volume => string.Equals(
                volume.Slug,
                volumeSlug,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<Destination?> GetDestinationAsync(
        string volumeSlug,
        string countrySlug,
        string destinationSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(countrySlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationSlug);

        var volumeDirectory = await FindVolumeDirectoryAsync(
            volumeSlug,
            cancellationToken);

        if (volumeDirectory is null)
        {
            return null;
        }

        var destinationPath = Path.Combine(
            volumeDirectory,
            "destinations",
            $"{destinationSlug}.json");

        var destination = await DeserializeFileAsync<Destination>(
            destinationPath,
            cancellationToken);

        if (destination is null)
        {
            return null;
        }

        return DestinationMatchesRoute(
            destination,
            volumeSlug,
            countrySlug,
            destinationSlug)
                ? destination
                : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Destination>> GetDestinationsForVolumeAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeSlug);

        var volume = await GetVolumeAsync(volumeSlug, cancellationToken);

        if (volume is null)
        {
            return [];
        }

        var volumeDirectory = await FindVolumeDirectoryAsync(
            volumeSlug,
            cancellationToken);

        if (volumeDirectory is null)
        {
            return [];
        }

        var destinations = new List<Destination>();

        foreach (var reference in volume.Destinations
            .OrderBy(item => item.DisplayOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.Combine(
                volumeDirectory,
                "destinations",
                $"{reference.DestinationSlug}.json");

            var destination = await DeserializeFileAsync<Destination>(
                destinationPath,
                cancellationToken);

            if (destination is not null && DestinationMatchesRoute(
                destination,
                volumeSlug,
                reference.CountrySlug,
                reference.DestinationSlug))
            {
                destinations.Add(destination);
            }
        }

        return destinations;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Destination>>
        GetPublishedDestinationsForVolumeAsync(
            string volumeSlug,
            CancellationToken cancellationToken = default)
    {
        var destinations = await GetDestinationsForVolumeAsync(
            volumeSlug,
            cancellationToken);

        return destinations
            .Where(destination => destination.Published)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Destination>> GetFeaturedDestinationsAsync(
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        var destinations = await GetPublishedDestinationsForVolumeAsync(
            volumeSlug,
            cancellationToken);

        return destinations
            .Where(destination => destination.Featured)
            .OrderBy(destination => destination.HomepageOrder)
            .ThenBy(destination => destination.Title)
            .ToArray();
    }

    private static bool DestinationMatchesRoute(
        Destination destination,
        string volumeSlug,
        string countrySlug,
        string destinationSlug)
    {
        return string.Equals(
                   destination.VolumeSlug,
                   volumeSlug,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   destination.CountrySlug,
                   countrySlug,
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   destination.Slug,
                   destinationSlug,
                   StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> FindVolumeDirectoryAsync(
        string volumeSlug,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_volumesDirectory))
        {
            return null;
        }

        foreach (var directory in Directory.EnumerateDirectories(_volumesDirectory))
        {
            var manifestPath = Path.Combine(directory, "volume.json");

            var volume = await DeserializeFileAsync<Volume>(
                manifestPath,
                cancellationToken);

            if (volume is not null
                && string.Equals(
                    volume.Slug,
                    volumeSlug,
                    StringComparison.OrdinalIgnoreCase))
            {
                return directory;
            }
        }

        return null;
    }

    private async Task<T?> DeserializeFileAsync<T>(
    string filePath,
    CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Length == 0)
        {
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);

            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                _serializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
    CancellationToken cancellationToken = default)
    {
        var volumes = await GetVolumesAsync(cancellationToken);

        return volumes
            .Where(volume => volume.Status.IsPubliclyVisible())
            .OrderBy(volume => volume.Number)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Volume?> GetCurrentVolumeAsync(
        CancellationToken cancellationToken = default)
    {
        var volumes = await GetVolumesAsync(cancellationToken);

        return volumes
            .Where(volume => volume.Status == VolumeStatus.Current)
            .OrderBy(volume => volume.Number)
            .FirstOrDefault();
    }
    /// <inheritdoc />
    public async Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
    string qrSlug,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrSlug))
        {
            return null;
        }

        var normalizedQrSlug = qrSlug.Trim();

        var volumes = await GetPublicVolumesAsync(cancellationToken);

        foreach (var volume in volumes)
        {
            var destinations =
                await GetPublishedDestinationsForVolumeAsync(
                    volume.Slug,
                    cancellationToken);

            foreach (var destination in destinations)
            {
                if (string.IsNullOrWhiteSpace(destination.QrSlug))
                {
                    continue;
                }

                if (!string.Equals(
                    destination.QrSlug,
                    normalizedQrSlug,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new QrDestinationRoute
                {
                    QrSlug = destination.QrSlug,
                    VolumeSlug = destination.VolumeSlug,
                    CountrySlug = destination.CountrySlug,
                    DestinationSlug = destination.Slug
                };
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<Journey?> GetJourneyAsync(
    string volumeSlug,
    string journeySlug,
    CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(journeySlug);

        var volumeDirectory = await FindVolumeDirectoryAsync(
            volumeSlug,
            cancellationToken);

        if (volumeDirectory is null)
        {
            return null;
        }

        var journeyPath = Path.Combine(
            volumeDirectory,
            "journeys",
            $"{journeySlug}.json");

        var journey = await DeserializeFileAsync<Journey>(
            journeyPath,
            cancellationToken);

        if (journey is null)
        {
            return null;
        }

        var routeMatches =
            string.Equals(
                journey.VolumeSlug,
                volumeSlug,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                journey.Slug,
                journeySlug,
                StringComparison.OrdinalIgnoreCase);

        return routeMatches ? journey : null;
    }
}
