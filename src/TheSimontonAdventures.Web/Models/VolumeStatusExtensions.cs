namespace TheSimontonAdventures.Web.Models;

public static class VolumeStatusExtensions
{
    public static bool IsPubliclyVisible(this VolumeStatus status)
    {
        return status is
            VolumeStatus.Planned
            or VolumeStatus.Upcoming
            or VolumeStatus.Current
            or VolumeStatus.Published;
    }

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