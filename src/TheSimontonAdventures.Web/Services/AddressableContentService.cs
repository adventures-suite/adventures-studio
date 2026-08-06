using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Routing;

namespace TheSimontonAdventures.Web.Services;

/// <summary>
/// Resolves stable public slugs to published AdventuresSuite content by using
/// the existing travel-content service as the initial content source.
/// </summary>
/// <remarks>
/// This service is the first implementation of the Address Engine abstraction.
///
/// It intentionally wraps <see cref="ITravelContentService"/> rather than
/// duplicating JSON-loading or file-system logic. This allows existing content
/// infrastructure to remain operational while public-address resolution evolves
/// into a reusable platform capability.
///
/// The initial implementation supports published destinations. Additional
/// addressable content types, including experiences, journeys, reflections,
/// quotes, media, and resources, will be added incrementally.
/// </remarks>
public sealed class AddressableContentService : IAddressableContentService
{
    /// <summary>
    /// Provides access to the existing JSON-backed travel content.
    /// </summary>
    private readonly ITravelContentService _travelContentService;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AddressableContentService"/> class.
    /// </summary>
    /// <param name="travelContentService">
    /// The existing content service used to retrieve volumes, destinations,
    /// and destination QR routes.
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
        string slug,
        CancellationToken cancellationToken = default)
    {
        // Empty public addresses are invalid and should not be passed to the
        // underlying content service.
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        // Normalize user-supplied input so route resolution is not affected by
        // accidental leading or trailing whitespace.
        var normalizedSlug = slug.Trim();

        var destinationRoute =
            await _travelContentService.GetDestinationRouteByQrSlugAsync(
                normalizedSlug,
                cancellationToken);

        if (destinationRoute is null)
        {
            return null;
        }

        var destination =
            await _travelContentService.GetDestinationAsync(
                destinationRoute.VolumeSlug,
                destinationRoute.CountrySlug,
                destinationRoute.DestinationSlug,
                cancellationToken);

        // The existing QR-route lookup identifies a destination route, but the
        // Content Engine remains authoritative for publication state and public
        // metadata.
        if (destination is null || !destination.Published)
        {
            return null;
        }

        return CreateDestinationRoute(destination);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AddressableContentRoute>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var publicVolumes =
            await _travelContentService.GetPublicVolumesAsync(
                cancellationToken);

        var routes = new List<AddressableContentRoute>();

        foreach (var volume in publicVolumes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinations =
                await _travelContentService
                    .GetPublishedDestinationsForVolumeAsync(
                        volume.Slug,
                        cancellationToken);

            foreach (var destination in destinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(destination.QrSlug))
                {
                    continue;
                }

                routes.Add(CreateDestinationRoute(destination));
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
    /// <param name="destination">
    /// The published destination to convert into an addressable route.
    /// </param>
    /// <returns>
    /// An <see cref="AddressableContentRoute"/> representing the destination's
    /// stable QR slug and current canonical application route.
    /// </returns>
    private static AddressableContentRoute CreateDestinationRoute(
        Destination destination)
    {
        return new AddressableContentRoute
        {
            Slug = destination.QrSlug,
            Title = destination.Title,
            ContentType = AddressableContentType.Destination,
            TargetUrl = TravelRoutes.Destination(
                destination.VolumeSlug,
                destination.CountrySlug,
                destination.Slug),
            Published = destination.Published,
            Aliases = []
        };
    }
}
