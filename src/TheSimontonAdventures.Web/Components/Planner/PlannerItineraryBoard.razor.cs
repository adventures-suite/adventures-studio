using AdventuresSuite.Planning;
using Microsoft.AspNetCore.Components;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Components;

/// <summary>Renders the already-authorized Adventure Plan detail as a responsive itinerary board.</summary>
public partial class PlannerItineraryBoard : ComponentBase
{
    /// <summary>Gets or sets the allowlisted, authorized plan projection.</summary>
    [Parameter, EditorRequired] public AdventurePlanDetail Plan { get; set; } = null!;
    /// <summary>Gets or sets whether existing mutation forms may be presented.</summary>
    [Parameter] public bool CanEdit { get; set; }
    /// <summary>Gets or sets the existing destination POST path.</summary>
    [Parameter, EditorRequired] public string AddDestinationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing itinerary-day POST path.</summary>
    [Parameter, EditorRequired] public string AddDayPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing activity POST path.</summary>
    [Parameter, EditorRequired] public string AddActivityPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing transportation POST path.</summary>
    [Parameter, EditorRequired] public string AddTransportationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing accommodation POST path.</summary>
    [Parameter, EditorRequired] public string AddAccommodationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing reservation-summary POST path.</summary>
    [Parameter, EditorRequired] public string AddReservationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the allowlisted destination PRG message.</summary>
    [Parameter] public string? DestinationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted day PRG message.</summary>
    [Parameter] public string? DayStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted activity PRG message.</summary>
    [Parameter] public string? ActivityStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted transportation PRG message.</summary>
    [Parameter] public string? TransportationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted accommodation PRG message.</summary>
    [Parameter] public string? AccommodationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted reservation PRG message.</summary>
    [Parameter] public string? ReservationStatusMessage { get; set; }

    private string? DestinationName(ItineraryDayDetail day) => day.DestinationVisitId is not { } id
        ? null
        : Plan.Destinations.FirstOrDefault(item => item.Id == id)?.Name;

    private static RenderFragment Status(string? message) => builder =>
    {
        if (message is null) return;
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "planner-board__status");
        builder.AddAttribute(2, "role", "status");
        builder.AddContent(3, message);
        builder.CloseElement();
    };

    private static string DateValue(DateOnly date) => date.ToString("yyyy-MM-dd");
    private static string FormatDate(DateOnly date) => date.ToString("MMM d, yyyy");
    private static string FormatDates(PlanningDateRange dates) => $"{FormatDate(dates.Start)} – {FormatDate(dates.End)}";
    private static string FormatOptionalTime(TimeOnly? time) => time is null ? string.Empty : $" at {time:h:mm tt}";
    private static string FormatTimeRange(TimeOnly? start, TimeOnly? end) => (start, end) switch
    {
        ({ } startTime, { } endTime) => $"{startTime:h:mm tt}–{endTime:h:mm tt}",
        ({ } startTime, null) => startTime.ToString("h:mm tt"),
        _ => "Time flexible"
    };
    private static string FormatStatus(PlanItemStatus status) => status.ToString();
}
