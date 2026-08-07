using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>Provides shared editorial presentation choices for destinations.</summary>
public static class DestinationPresentationExtensions
{
    /// <summary>Gets card-specific summary copy with the general summary as fallback.</summary>
    public static string GetCardSummary(this Destination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return !string.IsNullOrWhiteSpace(destination.HomepageSummary)
            ? destination.HomepageSummary
            : destination.Summary;
    }
}
