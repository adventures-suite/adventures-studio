using TheSimontonAdventures.Web.Routing;

namespace TheSimontonAdventures.Web.Models;

/// <summary>
/// Represents destination route identity discovered from a stable QR slug.
/// </summary>
public sealed class QrDestinationRoute
{
    /// <summary>Gets the stable public slug used by the QR address.</summary>
    public string QrSlug { get; init; } = string.Empty;

    /// <summary>Gets the destination's owning volume route segment.</summary>
    public string VolumeSlug { get; init; } = string.Empty;

    /// <summary>Gets the destination's country route segment.</summary>
    public string CountrySlug { get; init; } = string.Empty;

    /// <summary>Gets the destination route segment.</summary>
    public string DestinationSlug { get; init; } = string.Empty;

    /// <summary>Gets the canonical route assembled from the destination identity.</summary>
    public string DestinationUrl =>
        TravelRoutes.Destination(
            VolumeSlug,
            CountrySlug,
            DestinationSlug);
}
