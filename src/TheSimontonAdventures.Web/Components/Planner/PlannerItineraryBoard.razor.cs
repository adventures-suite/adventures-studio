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
    /// <summary>Gets or sets the current transient FootSteps context.</summary>
    [Parameter] public PlannerIdeasContext? SelectedContext { get; set; }
    /// <summary>Gets or sets the independently expanded Journey panels.</summary>
    [Parameter] public IReadOnlySet<PlannerWorkspacePanel> ExpandedPanels { get; set; } = new HashSet<PlannerWorkspacePanel>(Enum.GetValues<PlannerWorkspacePanel>());
    /// <summary>Gets or sets the panel selected by the exclusive focus toolbar.</summary>
    [Parameter] public PlannerWorkspacePanel FocusedPanel { get; set; }
    /// <summary>Gets or sets the callback raised when a panel header is manually toggled.</summary>
    [Parameter] public EventCallback<PlannerWorkspacePanel> OnPanelToggle { get; set; }
    /// <summary>Gets or sets the callback raised when destination or day context is selected.</summary>
    [Parameter] public EventCallback<PlannerIdeasContext> OnContextSelected { get; set; }
    /// <summary>Gets or sets whether a destination FootStep is currently being dragged.</summary>
    [Parameter] public bool IsDestinationFootStepDragging { get; set; }
    /// <summary>Gets or sets the destination FootStep awaiting explicit confirmation.</summary>
    [Parameter] public PlannerFootStepDefinition? PendingDestinationFootStep { get; set; }
    /// <summary>Gets or sets the callback raised when a destination FootStep is dropped on the route.</summary>
    [Parameter] public EventCallback OnDestinationFootStepDropped { get; set; }
    /// <summary>Gets or sets the callback that dismisses a pending destination FootStep review.</summary>
    [Parameter] public EventCallback OnDestinationFootStepReviewCancelled { get; set; }
    /// <summary>Gets or sets the protected Destination FootStep application path.</summary>
    [Parameter] public string ApplyDestinationFootStepPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing destination POST path.</summary>
    [Parameter, EditorRequired] public string AddDestinationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the protected destination-route reorder POST path.</summary>
    [Parameter, EditorRequired] public string ReorderDestinationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing itinerary-day POST path.</summary>
    [Parameter, EditorRequired] public string AddDayPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the itinerary-day edit POST path prefix.</summary>
    [Parameter, EditorRequired] public string EditDayPathPrefix { get; set; } = string.Empty;
    /// <summary>Gets or sets the safe plan-detail path used to cancel day editing.</summary>
    [Parameter, EditorRequired] public string DayCancelPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing activity POST path.</summary>
    [Parameter, EditorRequired] public string AddActivityPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the activity-edit POST path prefix.</summary>
    [Parameter, EditorRequired] public string EditActivityPathPrefix { get; set; } = string.Empty;
    /// <summary>Gets or sets the safe plan-detail path used to cancel activity editing.</summary>
    [Parameter, EditorRequired] public string ActivityCancelPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing transportation POST path.</summary>
    [Parameter, EditorRequired] public string AddTransportationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the transportation-edit POST path prefix.</summary>
    [Parameter, EditorRequired] public string EditTransportationPathPrefix { get; set; } = string.Empty;
    /// <summary>Gets or sets the safe plan-detail path used to cancel transportation editing.</summary>
    [Parameter, EditorRequired] public string TransportationCancelPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing accommodation POST path.</summary>
    [Parameter, EditorRequired] public string AddAccommodationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the accommodation-edit POST path prefix.</summary>
    [Parameter, EditorRequired] public string EditAccommodationPathPrefix { get; set; } = string.Empty;
    /// <summary>Gets or sets the safe plan-detail path used to cancel accommodation editing.</summary>
    [Parameter, EditorRequired] public string AccommodationCancelPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the existing reservation-summary POST path.</summary>
    [Parameter, EditorRequired] public string AddReservationPath { get; set; } = string.Empty;
    /// <summary>Gets or sets the allowlisted destination PRG message.</summary>
    [Parameter] public string? DestinationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted route-reorder PRG message.</summary>
    [Parameter] public string? RouteStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted day PRG message.</summary>
    [Parameter] public string? DayStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted itinerary-day edit PRG message.</summary>
    [Parameter] public string? DayEditStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted activity PRG message.</summary>
    [Parameter] public string? ActivityStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted activity-edit PRG message.</summary>
    [Parameter] public string? ActivityEditStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted transportation PRG message.</summary>
    [Parameter] public string? TransportationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted transportation-edit PRG message.</summary>
    [Parameter] public string? TransportationEditStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted accommodation PRG message.</summary>
    [Parameter] public string? AccommodationStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted accommodation-edit PRG message.</summary>
    [Parameter] public string? AccommodationEditStatusMessage { get; set; }
    /// <summary>Gets or sets the allowlisted reservation PRG message.</summary>
    [Parameter] public string? ReservationStatusMessage { get; set; }
    /// <summary>Gets or sets whether an Activity FootStep is currently being dragged.</summary>
    [Parameter] public bool IsActivityFootStepDragging { get; set; }
    /// <summary>Gets or sets the reviewed Activity FootStep and target selected by a drop.</summary>
    [Parameter] public PlannerActivityFootStepDrop? PendingActivityFootStep { get; set; }
    /// <summary>Gets or sets the callback that dismisses the pending Activity FootStep review.</summary>
    [Parameter] public EventCallback OnActivityFootStepReviewCancelled { get; set; }

    private string RouteCardClasses => $"planner-board__card planner-board__route{(IsDestinationFootStepDragging ? " planner-board__route--drop-ready" : string.Empty)}";
    private bool IsExpanded(PlannerWorkspacePanel panel) => ExpandedPanels.Contains(panel);
    private bool IsActivityFocus => FocusedPanel == PlannerWorkspacePanel.Activities;
    private bool IsFocused(PlannerWorkspacePanel panel) => FocusedPanel == panel
        || (panel == PlannerWorkspacePanel.Itinerary && IsActivityFocus);
    private string PanelClasses(PlannerWorkspacePanel panel, string baseClasses) =>
        $"{baseClasses} planner-board__panel{(IsFocused(panel) ? " planner-board__panel--focused" : IsExpanded(panel) ? " planner-board__panel--open" : " planner-board__panel--collapsed")}";
    private string RouteSummary => $"{Plan.Destinations.Count} destination{(Plan.Destinations.Count == 1 ? string.Empty : "s")}";
    private DestinationVisitDetail? SelectedDestination => SelectedContext?.Kind == PlannerIdeasContextKind.Destination
        ? Plan.Destinations.FirstOrDefault(destination => destination.Id.Value == SelectedContext.Id)
        : null;
    private IReadOnlyList<ItineraryDayDetail> VisibleDays => SelectedDestination is { } destination
        ? Plan.Days.Where(day => day.DestinationVisitId == destination.Id).ToArray()
        : Plan.Days;
    private IReadOnlyList<TransportationDetail> VisibleTransportation => SelectedDestination is { } destination
        ? Plan.Transportation.Where(segment =>
            segment.DepartureDestinationVisitId == destination.Id
            || segment.ArrivalDestinationVisitId == destination.Id).ToArray()
        : Plan.Transportation;
    private IReadOnlyList<AccommodationDetail> VisibleAccommodations => SelectedDestination is { } destination
        ? Plan.Accommodations.Where(stay => stay.DestinationVisitId == destination.Id).ToArray()
        : Plan.Accommodations;
    private IReadOnlyList<ReservationDetail> VisibleReservations => SelectedDestination is { } destination
        ? Plan.Reservations.Where(summary => summary.DestinationVisitId == destination.Id).ToArray()
        : Plan.Reservations;
    private string ItinerarySummary => SelectedDestination is { } destination
        ? $"{VisibleDays.Count} of {Plan.Days.Count} days · {destination.Name}"
        : IsActivityFocus
            ? $"{Plan.Days.Sum(day => day.Activities.Count)} activities across {Plan.Days.Count} days"
            : $"{Plan.Days.Count} day{(Plan.Days.Count == 1 ? string.Empty : "s")}";
    private string TransportationSummary => SelectedDestination is { } destination
        ? $"{VisibleTransportation.Count} of {Plan.Transportation.Count} segments · {destination.Name}"
        : $"{Plan.Transportation.Count} segment{(Plan.Transportation.Count == 1 ? string.Empty : "s")}";
    private string AccommodationSummary => SelectedDestination is { } destination
        ? $"{VisibleAccommodations.Count} of {Plan.Accommodations.Count} stays · {destination.Name}"
        : $"{Plan.Accommodations.Count} stay{(Plan.Accommodations.Count == 1 ? string.Empty : "s")}";
    private string ReservationSummary => SelectedDestination is { } destination
        ? $"{VisibleReservations.Count} of {Plan.Reservations.Count} summaries · {destination.Name}"
        : $"{Plan.Reservations.Count} summar{(Plan.Reservations.Count == 1 ? "y" : "ies")}";
    private Task DropDestinationFootStepAsync() => OnDestinationFootStepDropped.InvokeAsync();
    private DestinationVisitId? draggedDestinationVisitId;
    private int? destinationDropTargetSequence;
    private DestinationVisitDetail? pendingDestinationMove;
    private int pendingDestinationSequence;

    private void BeginDestinationMove(DestinationVisitId destinationVisitId) =>
        draggedDestinationVisitId = destinationVisitId;

    private void HighlightDestinationDrop(int targetSequence) =>
        destinationDropTargetSequence = draggedDestinationVisitId.HasValue
            ? targetSequence
            : null;

    private void EndDestinationDrag()
    {
        draggedDestinationVisitId = null;
        destinationDropTargetSequence = null;
    }

    private void ReviewDestinationDrop(int targetSequence)
    {
        if (draggedDestinationVisitId is { } id)
        {
            ReviewDestinationMove(id, targetSequence);
        }

        EndDestinationDrag();
    }

    private void ReviewDestinationMove(DestinationVisitId id, int targetSequence)
    {
        pendingDestinationMove = Plan.Destinations.SingleOrDefault(item => item.Id == id);
        pendingDestinationSequence = targetSequence;
    }

    private void CancelDestinationMove()
    {
        pendingDestinationMove = null;
        pendingDestinationSequence = 0;
        EndDestinationDrag();
    }

    private string DestinationCardClasses(DestinationVisitDetail destination) =>
        $"{(IsSelected(destination) ? "planner-board__selected" : string.Empty)}{(destinationDropTargetSequence == destination.Sequence ? " planner-board__drop-before" : string.Empty)}".Trim();

    private string? DestinationName(ItineraryDayDetail day) => day.DestinationVisitId is not { } id
        ? null
        : Plan.Destinations.FirstOrDefault(item => item.Id == id)?.Name;

    private bool IsSelected(DestinationVisitDetail destination) =>
        SelectedContext?.Kind == PlannerIdeasContextKind.Destination
        && SelectedContext.Id == destination.Id.Value;

    private bool IsSelected(ItineraryDayDetail day) =>
        SelectedContext?.Kind == PlannerIdeasContextKind.Day
        && SelectedContext.Id == day.Id.Value;

    private Task SelectDestination(DestinationVisitDetail destination) =>
        OnContextSelected.InvokeAsync(new PlannerIdeasContext(PlannerIdeasContextKind.Destination, destination.Id.Value, destination.Name));

    private Task ShowEntireJourneyAsync() =>
        OnContextSelected.InvokeAsync(new PlannerIdeasContext(PlannerIdeasContextKind.Adventure, Plan.Id.Value, Plan.Title));

    private Task SelectDay(ItineraryDayDetail day) =>
        OnContextSelected.InvokeAsync(new PlannerIdeasContext(PlannerIdeasContextKind.Day, day.Id.Value, day.Title));

    private string EditActivityPath(PlannedActivityId activityId) =>
        $"{EditActivityPathPrefix}/{activityId.Value}/edit";

    private string EditDayPath(ItineraryDayId dayId) =>
        $"{EditDayPathPrefix}/{dayId.Value}/edit";

    private string EditTransportationPath(TransportationSegmentId segmentId) =>
        $"{EditTransportationPathPrefix}/{segmentId.Value}/edit";

    private string EditAccommodationPath(AccommodationId accommodationId) =>
        $"{EditAccommodationPathPrefix}/{accommodationId.Value}/edit";

    private static string DayFieldId(ItineraryDayId dayId, string field) =>
        $"day-{dayId.Value}-{field}";

    private static string ActivityFieldId(PlannedActivityId activityId, string field) =>
        $"activity-{activityId.Value}-{field}";

    private static string TransportationFieldId(TransportationSegmentId segmentId, string field) =>
        $"transportation-{segmentId.Value}-{field}";

    private static string AccommodationFieldId(AccommodationId accommodationId, string field) =>
        $"accommodation-{accommodationId.Value}-{field}";

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
    private static string? TimeValue(TimeOnly? time) => time?.ToString("HH:mm");
    private static string FormatTimeRange(TimeOnly? start, TimeOnly? end) => (start, end) switch
    {
        ({ } startTime, { } endTime) => $"{startTime:h:mm tt}–{endTime:h:mm tt}",
        ({ } startTime, null) => startTime.ToString("h:mm tt"),
        _ => "Time flexible"
    };
    private static string FormatStatus(PlanItemStatus status) => status.ToString();
}
