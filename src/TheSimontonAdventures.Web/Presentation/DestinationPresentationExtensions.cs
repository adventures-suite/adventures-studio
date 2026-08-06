using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>
/// Provides shared presentation choices for destination content.
/// </summary>
public static class DestinationPresentationExtensions
{
    /// <summary>
    /// Selects the destination image intended for cards and featured listings.
    /// </summary>
    /// <param name="destination">The destination being presented.</param>
    /// <returns>
    /// The homepage-specific image when supplied; otherwise, the hero image.
    /// </returns>
    public static string GetCardImage(this Destination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return !string.IsNullOrWhiteSpace(destination.HomepageImage)
            ? destination.HomepageImage
            : destination.HeroImage;
    }

    /// <summary>
    /// Selects the destination summary intended for cards and featured listings.
    /// </summary>
    /// <param name="destination">The destination being presented.</param>
    /// <returns>
    /// The homepage-specific summary when supplied; otherwise, the general
    /// destination summary.
    /// </returns>
    public static string GetCardSummary(this Destination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return !string.IsNullOrWhiteSpace(destination.HomepageSummary)
            ? destination.HomepageSummary
            : destination.Summary;
    }
}
