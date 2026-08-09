using System.Globalization;
using TheSimontonAdventures.Web.Models;

namespace TheSimontonAdventures.Web.Presentation;

/// <summary>Formats typed Journey visit schedules for Creator presentation.</summary>
public static class JourneyVisitSchedulePresentationExtensions
{
    /// <summary>Formats the local visit date range in the Creator locale.</summary>
    /// <param name="schedule">The Journey-owned local visit schedule.</param>
    /// <param name="creatorLocale">The resolved Creator locale.</param>
    /// <returns>A localized date or date range.</returns>
    public static string FormatDateRange(
        this JourneyVisitSchedule schedule,
        string creatorLocale)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.PlannedArrivalDate is null
            || schedule.PlannedDepartureDate is null)
        {
            return string.Empty;
        }

        var culture = CultureInfo.GetCultureInfo(creatorLocale);
        return schedule.PlannedArrivalDate == schedule.PlannedDepartureDate
            ? schedule.PlannedArrivalDate.Value.ToString("d", culture)
            : $"{schedule.PlannedArrivalDate.Value.ToString("d", culture)} " +
              $"\u2013 {schedule.PlannedDepartureDate.Value.ToString("d", culture)}";
    }

    /// <summary>Formats the optional gangway window in the Creator locale.</summary>
    /// <param name="schedule">The Journey-owned local visit schedule.</param>
    /// <param name="creatorLocale">The resolved Creator locale.</param>
    /// <returns>A localized gangway window, or an empty string when unknown.</returns>
    public static string FormatGangwayWindow(
        this JourneyVisitSchedule schedule,
        string creatorLocale)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.PlannedGangwayDownTime is null
            || schedule.PlannedGangwayUpTime is null)
        {
            return string.Empty;
        }

        var culture = CultureInfo.GetCultureInfo(creatorLocale);
        return "Gangway down " +
            schedule.PlannedGangwayDownTime.Value.ToString("t", culture) +
            " · Gangway up " +
            schedule.PlannedGangwayUpTime.Value.ToString("t", culture);
    }
}
