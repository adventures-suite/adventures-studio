using System.Globalization;
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

    /// <summary>Formats the planned travel range using the Creator's locale.</summary>
    /// <param name="destination">The destination being presented.</param>
    /// <param name="creatorLocale">The resolved Creator's locale identifier.</param>
    /// <returns>A localized date range, or an empty string when incomplete.</returns>
    public static string FormatPlannedDateRange(
        this Destination destination,
        string creatorLocale)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return FormatDateRange(
            destination.PlannedArrivalDate,
            destination.PlannedDepartureDate,
            creatorLocale);
    }

    /// <summary>Formats the actual visit range using the Creator's locale.</summary>
    /// <param name="destination">The destination being presented.</param>
    /// <param name="creatorLocale">The resolved Creator's locale identifier.</param>
    /// <returns>A localized date range, or an empty string when incomplete.</returns>
    public static string FormatVisitedDateRange(
        this Destination destination,
        string creatorLocale)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return FormatDateRange(
            destination.VisitedFrom,
            destination.VisitedTo,
            creatorLocale);
    }

    private static string FormatDateRange(
        DateOnly? from,
        DateOnly? to,
        string creatorLocale)
    {
        if (from is null || to is null)
        {
            return string.Empty;
        }

        var culture = CultureInfo.GetCultureInfo(creatorLocale);
        return from == to
            ? from.Value.ToString("d", culture)
            : $"{from.Value.ToString("d", culture)} \u2013 {to.Value.ToString("d", culture)}";
    }
}
