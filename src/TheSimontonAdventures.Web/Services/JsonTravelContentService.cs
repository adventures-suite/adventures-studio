using System.Text.Json;
using System.Text.Json.Serialization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Loads strongly typed travel content from each Creator's validated JSON
/// content root.
/// </summary>
public sealed class JsonTravelContentService : ITravelContentService
{
    private readonly string _applicationContentRoot;
    private readonly ICreatorService _creatorService;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>Initializes Creator-scoped content access.</summary>
    /// <param name="hostEnvironment">The active application host environment.</param>
    /// <param name="creatorService">The validated Creator manifest service.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public JsonTravelContentService(
        IHostEnvironment hostEnvironment,
        ICreatorService creatorService)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(creatorService);

        _applicationContentRoot = hostEnvironment.ContentRootPath;
        _creatorService = creatorService;
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
    public async Task<CreatorProfile?> GetCreatorProfileAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        var contentRoot = await ResolveVolumesDirectoryAsync(
            creatorId,
            cancellationToken);

        if (contentRoot is null)
        {
            return null;
        }

        var profilePath = Path.GetFullPath(Path.Combine(
            contentRoot,
            "profile.json"));
        EnsurePathWithinRoot(profilePath, contentRoot);
        return await DeserializeFileAsync<CreatorProfile>(
            profilePath,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Volume>> GetVolumesAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        var volumesDirectory = await ResolveVolumesDirectoryAsync(
            creatorId,
            cancellationToken);

        if (volumesDirectory is null)
        {
            return [];
        }

        var volumes = new List<Volume>();

        foreach (var manifestPath in Directory
            .EnumerateFiles(
                volumesDirectory,
                "volume.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<Volume?> GetVolumeAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        ValidateRouteSlug(volumeSlug, nameof(volumeSlug));
        var volumes = await GetVolumesAsync(creatorId, cancellationToken);

        return volumes.FirstOrDefault(volume => string.Equals(
            volume.Slug,
            volumeSlug,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<Destination?> GetDestinationAsync(
        CreatorId creatorId,
        string volumeSlug,
        string countrySlug,
        string destinationSlug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumeSlug)
            || string.IsNullOrWhiteSpace(countrySlug)
            || string.IsNullOrWhiteSpace(destinationSlug))
        {
            return null;
        }

        ValidateRouteSlug(volumeSlug, nameof(volumeSlug));
        ValidateRouteSlug(countrySlug, nameof(countrySlug));
        ValidateRouteSlug(destinationSlug, nameof(destinationSlug));

        var volumeDirectory = await FindVolumeDirectoryAsync(
            creatorId,
            volumeSlug,
            cancellationToken);

        if (volumeDirectory is null)
        {
            return null;
        }

        var destinationPath = BuildJsonContentPath(
            volumeDirectory,
            "destinations",
            destinationSlug);
        var destination = await DeserializeFileAsync<Destination>(
            destinationPath,
            cancellationToken);

        return destination is not null && DestinationMatchesRoute(
            destination,
            volumeSlug,
            countrySlug,
            destinationSlug)
                ? destination
                : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Destination>> GetDestinationsForVolumeAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        ValidateRouteSlug(volumeSlug, nameof(volumeSlug));
        var volume = await GetVolumeAsync(
            creatorId,
            volumeSlug,
            cancellationToken);

        if (volume is null)
        {
            return [];
        }

        var volumeDirectory = await FindVolumeDirectoryAsync(
            creatorId,
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

            // Planned manifests may reserve itinerary positions before the
            // corresponding destination route has been authored.
            if (string.IsNullOrWhiteSpace(reference.CountrySlug)
                || string.IsNullOrWhiteSpace(reference.DestinationSlug))
            {
                continue;
            }

            ValidateRouteSlug(
                reference.CountrySlug,
                nameof(reference.CountrySlug));
            ValidateRouteSlug(
                reference.DestinationSlug,
                nameof(reference.DestinationSlug));

            var destinationPath = BuildJsonContentPath(
                volumeDirectory,
                "destinations",
                reference.DestinationSlug);
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
            CreatorId creatorId,
            string volumeSlug,
            CancellationToken cancellationToken = default)
    {
        var destinations = await GetDestinationsForVolumeAsync(
            creatorId,
            volumeSlug,
            cancellationToken);

        return destinations
            .Where(destination => destination.Published)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Destination>> GetFeaturedDestinationsAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken = default)
    {
        var destinations = await GetPublishedDestinationsForVolumeAsync(
            creatorId,
            volumeSlug,
            cancellationToken);

        return destinations
            .Where(destination => destination.Featured)
            .OrderBy(destination => destination.HomepageOrder)
            .ThenBy(destination => destination.Title)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Volume>> GetPublicVolumesAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        var volumes = await GetVolumesAsync(creatorId, cancellationToken);

        return volumes
            .Where(volume => volume.Status.IsPubliclyVisible())
            .OrderBy(volume => volume.Number)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<Volume?> GetCurrentVolumeAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        var volumes = await GetVolumesAsync(creatorId, cancellationToken);

        return volumes
            .Where(volume => volume.Status == VolumeStatus.Current)
            .OrderBy(volume => volume.Number)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<QrDestinationRoute?> GetDestinationRouteByQrSlugAsync(
        CreatorId creatorId,
        string qrSlug,
        CancellationToken cancellationToken = default)
    {
        ValidateRouteSlug(qrSlug, nameof(qrSlug));
        var volumes = await GetPublicVolumesAsync(creatorId, cancellationToken);

        foreach (var volume in volumes)
        {
            var destinations = await GetPublishedDestinationsForVolumeAsync(
                creatorId,
                volume.Slug,
                cancellationToken);

            foreach (var destination in destinations)
            {
                var matchesPrimary = string.Equals(
                    destination.QrSlug,
                    qrSlug,
                    StringComparison.OrdinalIgnoreCase);
                var matchesAlias = destination.QrAliases.Any(alias =>
                    string.Equals(
                        alias,
                        qrSlug,
                        StringComparison.OrdinalIgnoreCase));

                if (matchesPrimary || matchesAlias)
                {
                    return new QrDestinationRoute
                    {
                        QrSlug = destination.QrSlug,
                        VolumeSlug = destination.VolumeSlug,
                        CountrySlug = destination.CountrySlug,
                        DestinationSlug = destination.Slug
                    };
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<Journey?> GetJourneyAsync(
        CreatorId creatorId,
        string volumeSlug,
        string journeySlug,
        CancellationToken cancellationToken = default)
    {
        ValidateRouteSlug(volumeSlug, nameof(volumeSlug));
        ValidateRouteSlug(journeySlug, nameof(journeySlug));

        var volumeDirectory = await FindVolumeDirectoryAsync(
            creatorId,
            volumeSlug,
            cancellationToken);

        if (volumeDirectory is null)
        {
            return null;
        }

        var journeyPath = BuildJsonContentPath(
            volumeDirectory,
            "journeys",
            journeySlug);
        var journey = await DeserializeFileAsync<Journey>(
            journeyPath,
            cancellationToken);

        return journey is not null
            && string.Equals(
                journey.VolumeSlug,
                volumeSlug,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                journey.Slug,
                journeySlug,
                StringComparison.OrdinalIgnoreCase)
                ? journey
                : null;
    }

    private async Task<string?> ResolveVolumesDirectoryAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken)
    {
        EnsureCreatorScope(creatorId);
        var creator = await _creatorService.GetByIdAsync(
            creatorId,
            cancellationToken);

        if (creator is null)
        {
            return null;
        }

        CreatorManifestValidator.Validate(creator, _applicationContentRoot);
        var resolvedContentRoot = Path.GetFullPath(
            Path.Combine(_applicationContentRoot, creator.ContentRoot));
        EnsurePathWithinRoot(resolvedContentRoot, _applicationContentRoot);
        return resolvedContentRoot;
    }

    private async Task<string?> FindVolumeDirectoryAsync(
        CreatorId creatorId,
        string volumeSlug,
        CancellationToken cancellationToken)
    {
        var volumesDirectory = await ResolveVolumesDirectoryAsync(
            creatorId,
            cancellationToken);

        if (volumesDirectory is null)
        {
            return null;
        }

        foreach (var manifestPath in Directory.EnumerateFiles(
            volumesDirectory,
            "volume.json",
            SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var volume = await DeserializeFileAsync<Volume>(
                manifestPath,
                cancellationToken);

            if (volume is not null && string.Equals(
                volume.Slug,
                volumeSlug,
                StringComparison.OrdinalIgnoreCase))
            {
                var volumeDirectory = Path.GetDirectoryName(manifestPath)!;
                EnsurePathWithinRoot(volumeDirectory, volumesDirectory);
                return volumeDirectory;
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

        if (new FileInfo(filePath).Length == 0)
        {
            throw new InvalidDataException(
                $"Content file '{filePath}' is empty.");
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var content = await JsonSerializer.DeserializeAsync<T>(
                stream,
                _serializerOptions,
                cancellationToken);

            return content ?? throw new InvalidDataException(
                $"Content file '{filePath}' contains no object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Content file '{filePath}' contains invalid JSON.",
                exception);
        }
    }

    private static string BuildJsonContentPath(
        string volumeDirectory,
        string contentDirectory,
        string slug)
    {
        ValidateRouteSlug(slug, nameof(slug));
        var resolvedPath = Path.GetFullPath(Path.Combine(
            volumeDirectory,
            contentDirectory,
            $"{slug}.json"));
        EnsurePathWithinRoot(resolvedPath, volumeDirectory);
        return resolvedPath;
    }

    private static void EnsureCreatorScope(CreatorId creatorId)
    {
        if (creatorId == default)
        {
            throw new ArgumentException(
                "A non-default Creator identity is required.",
                nameof(creatorId));
        }
    }

    private static void ValidateRouteSlug(string slug, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(slug)
            || slug.Length > 100
            || !IsLowercaseLetter(slug[0])
            || !IsLowercaseLetterOrDigit(slug[^1])
            || slug.Any(character => character is not (
                >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-')))
        {
            throw new ArgumentException(
                "Content slugs must be lowercase route segments containing only " +
                "letters, digits, and hyphens.",
                parameterName);
        }
    }

    private static bool IsLowercaseLetter(char character) =>
        character is >= 'a' and <= 'z';

    private static bool IsLowercaseLetterOrDigit(char character) =>
        IsLowercaseLetter(character) || character is >= '0' and <= '9';

    private static void EnsurePathWithinRoot(string path, string trustedRoot)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(trustedRoot));
        var normalizedPath = Path.GetFullPath(path);
        var rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!normalizedPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidDataException(
                "Resolved content path is outside the Creator content root.");
        }
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
}
