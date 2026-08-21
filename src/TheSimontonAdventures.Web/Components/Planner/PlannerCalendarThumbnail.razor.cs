using Microsoft.AspNetCore.Components;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders compact, accessible month calendars for an inclusive itinerary date range.</summary>
public partial class PlannerCalendarThumbnail
{
    private static readonly string[] WeekdayLabels = ["S", "M", "T", "W", "T", "F", "S"];

    /// <summary>Gets or sets the authoritative first local date to highlight.</summary>
    [Parameter, EditorRequired]
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the optional inclusive final local date to highlight.</summary>
    [Parameter]
    public DateOnly? EndDate { get; set; }

    private DateOnly RangeEnd => EndDate is { } end && end >= Date ? end : Date;
    private string AccessibleLabel => RangeEnd == Date
        ? $"{Date:MMMM yyyy} calendar, {Date:MMMM d} selected"
        : $"Calendar, {Date:MMMM d, yyyy} through {RangeEnd:MMMM d, yyyy} selected";

    private IReadOnlyList<DateOnly> VisibleMonths
    {
        get
        {
            var month = new DateOnly(Date.Year, Date.Month, 1);
            var finalMonth = new DateOnly(RangeEnd.Year, RangeEnd.Month, 1);
            var months = new List<DateOnly>();
            while (month <= finalMonth)
            {
                months.Add(month);
                month = month.AddMonths(1);
            }

            return months;
        }
    }

    private static IReadOnlyList<int?> CalendarDays(DateOnly month)
    {
        var leadingBlanks = (int)month.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        return Enumerable.Repeat<int?>(null, leadingBlanks)
            .Concat(Enumerable.Range(1, daysInMonth).Select(day => (int?)day))
            .ToArray();
    }

    private bool IsSelected(DateOnly date) => date >= Date && date <= RangeEnd;
}
