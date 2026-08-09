using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>
/// Centralizes publication rules applied by public Razor routes.
/// </summary>
public static class PublicContentPolicy
{
    /// <summary>Determines whether a volume may be rendered publicly.</summary>
    /// <param name="volume">The candidate volume.</param>
    /// <returns>Whether the volume has a publicly visible lifecycle state.</returns>
    public static bool IsPublic(Volume? volume) =>
        volume is not null && volume.Status.IsPubliclyVisible();

    /// <summary>Determines whether a destination may be rendered publicly.</summary>
    /// <param name="volume">The destination's owning volume.</param>
    /// <param name="destination">The candidate destination.</param>
    /// <returns>Whether both ownership levels are publicly visible.</returns>
    public static bool IsPublic(Volume? volume, Destination? destination) =>
        IsPublic(volume) && destination is { Published: true };

    /// <summary>Determines whether a journey may be rendered publicly.</summary>
    /// <param name="volume">The journey's owning volume.</param>
    /// <param name="journey">The candidate journey.</param>
    /// <returns>Whether both ownership levels are publicly visible.</returns>
    public static bool IsPublic(Volume? volume, Journey? journey) =>
        IsPublic(volume) && journey is { Published: true };
}
