namespace TheSimontonAdventures.Web.Routing;

/// <summary>
/// Builds canonical application routes for travel content.
/// </summary>
/// <remarks>
/// Centralizing route construction prevents links produced by components,
/// models, and services from drifting as the public URL structure evolves.
/// </remarks>
public static class TravelRoutes
{
    /// <summary>
    /// Builds the canonical route for a volume.
    /// </summary>
    /// <param name="volumeSlug">The volume's public slug.</param>
    /// <returns>A root-relative, URL-escaped volume route.</returns>
    public static string Volume(string volumeSlug)
    {
        return $"/volumes/{EscapeSegment(volumeSlug, nameof(volumeSlug))}";
    }

    /// <summary>
    /// Builds the canonical route for a destination.
    /// </summary>
    /// <param name="volumeSlug">The containing volume's public slug.</param>
    /// <param name="countrySlug">The destination country's public slug.</param>
    /// <param name="destinationSlug">The destination's public slug.</param>
    /// <returns>A root-relative, URL-escaped destination route.</returns>
    public static string Destination(
        string volumeSlug,
        string countrySlug,
        string destinationSlug)
    {
        return $"{Volume(volumeSlug)}/" +
               $"{EscapeSegment(countrySlug, nameof(countrySlug))}/" +
               EscapeSegment(destinationSlug, nameof(destinationSlug));
    }

    private static string EscapeSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A route segment is required.",
                parameterName);
        }

        return Uri.EscapeDataString(value.Trim());
    }
}
