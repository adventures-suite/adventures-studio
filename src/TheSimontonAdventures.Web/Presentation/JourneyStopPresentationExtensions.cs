using TheSimontonAdventures.Web.Models;
using TheSimontonAdventures.Web.Routing;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>
/// Provides shared ordering and navigation behavior for journey-stop displays.
/// </summary>
public static class JourneyStopPresentationExtensions
{
    /// <summary>
    /// Orders journey stops by their declared display position.
    /// </summary>
    /// <param name="stops">The journey stops being presented.</param>
    /// <returns>The journey stops in ascending display order.</returns>
    public static IEnumerable<JourneyStop> InDisplayOrder(
        this IEnumerable<JourneyStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        return stops.OrderBy(stop => stop.DisplayOrder);
    }

    /// <summary>
    /// Builds the destination route for a linked journey stop.
    /// </summary>
    /// <param name="stop">The journey stop being presented.</param>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <returns>
    /// The canonical destination route, or an empty string when the stop does
    /// not identify a destination.
    /// </returns>
    public static string GetDestinationRoute(
        this JourneyStop stop,
        string volumeSlug)
    {
        ArgumentNullException.ThrowIfNull(stop);

        if (string.IsNullOrWhiteSpace(stop.CountrySlug)
            || string.IsNullOrWhiteSpace(stop.DestinationSlug))
        {
            return string.Empty;
        }

        return TravelRoutes.Destination(
            volumeSlug,
            stop.CountrySlug,
            stop.DestinationSlug);
    }
}
