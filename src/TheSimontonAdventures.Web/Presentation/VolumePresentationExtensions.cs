using System.Globalization;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>
/// Provides shared display formatting for travel volumes.
/// </summary>
public static class VolumePresentationExtensions
{
    /// <summary>
    /// Formats the volume number for public presentation.
    /// </summary>
    /// <param name="volume">The volume being presented.</param>
    /// <returns>
    /// A Roman numeral for supported volume numbers one through five;
    /// otherwise, the invariant numeric representation.
    /// </returns>
    public static string GetNumberLabel(this Volume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return volume.Number switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => volume.Number.ToString(CultureInfo.InvariantCulture)
        };
    }
}
