using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Routing;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Resolves Creator-scoped public slugs to published AdventuresSuite content.
/// </summary>
/// <remarks>
/// This service is the first implementation of the Address Engine abstraction.
///
/// It intentionally uses the Creator-scoped <see cref="ITravelContentService"/>
/// rather than duplicating JSON-loading or file-system logic.
///
/// The initial implementation supports published destinations. Additional
/// addressable content types, including experiences, journeys, reflections,
/// quotes, media, and resources, will be added incrementally.
/// </remarks>
public sealed class AddressableContentService : IAddressableContentService
{
    /// <summary>
    /// Provides Creator-scoped access to published travel content.
    /// </summary>
    private readonly ITravelContentService _travelContentService;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AddressableContentService"/> class.
    /// </summary>
    /// <param name="travelContentService">
    /// The Creator-scoped Content Engine used to retrieve published
    /// destinations.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="travelContentService"/> is
    /// <see langword="null"/>.
    /// </exception>
    public AddressableContentService(
        ITravelContentService travelContentService)
    {
        ArgumentNullException.ThrowIfNull(travelContentService);

        _travelContentService = travelContentService;
    }

    /// <inheritdoc />
    public async Task<AddressableContentRoute?> ResolveAsync(
        CreatorId creatorId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        EnsureCreatorScope(creatorId);

        // Empty public addresses are invalid and should not be passed to the
        // underlying content service.
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        if (!TryNormalizeRequestedSlug(slug, out var normalizedSlug))
        {
            return null;
        }

        var routes = await GetAllAsync(creatorId, cancellationToken);

        return routes.FirstOrDefault(route =>
            string.Equals(
                route.Slug,
                normalizedSlug,
                StringComparison.OrdinalIgnoreCase)
            || route.Aliases.Any(alias => string.Equals(
                alias,
                normalizedSlug,
                StringComparison.OrdinalIgnoreCase)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AddressableContentRoute>> GetAllAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        EnsureCreatorScope(creatorId);
        var publicVolumes = await _travelContentService.GetPublicVolumesAsync(
            creatorId,
            cancellationToken);
        var routes = new List<AddressableContentRoute>();
        var registeredSlugs = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var volume in publicVolumes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinations = await _travelContentService
                .GetPublishedDestinationsForVolumeAsync(
                    creatorId,
                    volume.Slug,
                    cancellationToken);

            foreach (var destination in destinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!destination.Published)
                {
                    continue;
                }

                var route = CreateDestinationRoute(creatorId, destination);
                RegisterSlug(registeredSlugs, route.Slug, route.Slug);

                foreach (var alias in route.Aliases)
                {
                    RegisterSlug(registeredSlugs, alias, route.Slug);
                }

                routes.Add(route);
            }
        }

        // Return deterministic results so diagnostics, QR manifests, tests, and
        // future administrative tools receive stable ordering.
        return routes
            .OrderBy(route => route.Slug, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Creates a platform-neutral public route from an existing destination
    /// content model.
    /// </summary>
    /// <param name="creatorId">The Creator that owns the destination.</param>
    /// <param name="destination">
    /// The published destination to convert into an addressable route.
    /// </param>
    /// <returns>
    /// An <see cref="AddressableContentRoute"/> representing the destination's
    /// stable QR slug and current canonical application route.
    /// </returns>
    private static AddressableContentRoute CreateDestinationRoute(
        CreatorId creatorId,
        Destination destination)
    {
        var primarySlug = NormalizeContentSlug(
            destination.QrSlug,
            destination.Slug);
        var aliases = destination.QrAliases
            .Select(alias => NormalizeContentSlug(alias, destination.Slug))
            .ToArray();

        return new AddressableContentRoute
        {
            CreatorId = creatorId,
            Slug = primarySlug,
            Title = destination.Title,
            ContentType = AddressableContentType.Destination,
            TargetUrl = TravelRoutes.Destination(
                destination.VolumeSlug,
                destination.CountrySlug,
                destination.Slug),
            Published = destination.Published,
            Aliases = aliases
        };
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

    private static bool TryNormalizeRequestedSlug(
        string slug,
        out string normalizedSlug)
    {
        normalizedSlug = slug.Trim().Trim('/');

        return normalizedSlug.Length > 0
            && !normalizedSlug.Contains('/')
            && !normalizedSlug.Contains('\\')
            && !normalizedSlug.Contains('?')
            && !normalizedSlug.Contains('#');
    }

    private static string NormalizeContentSlug(
        string slug,
        string destinationSlug)
    {
        if (!TryNormalizeRequestedSlug(slug, out var normalizedSlug))
        {
            throw new InvalidDataException(
                $"Destination '{destinationSlug}' has an invalid public slug.");
        }

        return normalizedSlug;
    }

    private static void RegisterSlug(
        IDictionary<string, string> registeredSlugs,
        string slug,
        string primarySlug)
    {
        if (!registeredSlugs.TryAdd(slug, primarySlug))
        {
            throw new InvalidDataException(
                $"Public slug or alias '{slug}' is duplicated within the Creator.");
        }
    }
}
