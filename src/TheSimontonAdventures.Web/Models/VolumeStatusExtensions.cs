namespace TheSimontonAdventures.Web.Models;

/// <summary>Provides publication and presentation behavior for volume states.</summary>
public static class VolumeStatusExtensions
{
    /// <summary>Determines whether a volume state may be shown publicly.</summary>
    /// <param name="status">The lifecycle state being evaluated.</param>
    /// <returns><see langword="true"/> when the state is publicly visible.</returns>
    public static bool IsPubliclyVisible(this VolumeStatus status)
    {
        return status is
            VolumeStatus.Planned
            or VolumeStatus.Upcoming
            or VolumeStatus.Current
            or VolumeStatus.Published;
    }

    /// <summary>Gets the editorial label displayed for a volume state.</summary>
    /// <param name="status">The lifecycle state being formatted.</param>
    /// <returns>A concise public-facing status label.</returns>
    public static string GetDisplayLabel(this VolumeStatus status)
    {
        return status switch
        {
            VolumeStatus.Planned => "Planned Adventure",
            VolumeStatus.Upcoming => "Coming Soon",
            VolumeStatus.Current => "Current Journey",
            VolumeStatus.Published => "Published Journey",
            _ => "Draft"
        };
    }
}
