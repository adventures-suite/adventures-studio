using AdventuresSuite.Identity;
using AdventuresSuite.Identity.ExternalId;
using AdventuresSuite.Planning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Components;

/// <summary>
/// Renders and coordinates the authenticated private Planner workspace.
/// </summary>
public partial class WorkspaceRoot
{
    /// <summary>
    /// Gets or sets the workspace request path captured before the interactive circuit starts.
    /// </summary>
    [Parameter]
    public string InitialPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets the workspace query string captured before the interactive circuit starts.
    /// </summary>
    [Parameter]
    public string InitialQueryString { get; set; } = string.Empty;

    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

    private IReadOnlyList<AdventurePlanDashboardItem> Plans { get; set; } = [];
    private AdventurePlanDetail? Plan { get; set; }
    private bool PlanCanEdit { get; set; }
    private CreatorId AddressedCreatorId { get; set; }
    private WorkspaceLoadState LoadState { get; set; } = WorkspaceLoadState.Landing;
    private string CreateIdempotencyKey { get; } = $"request_{Guid.NewGuid():N}";
    private PlannerIdeasContext? SelectedIdeasContext { get; set; }
    private int IdeasWidthPixels { get; set; } = 320;

    private Task SelectIdeasContextAsync(PlannerIdeasContext context)
    {
        SelectedIdeasContext = context;
        return Task.CompletedTask;
    }

    private Task ResizeIdeasAsync(int requestedWidthPixels)
    {
        IdeasWidthPixels = Math.Clamp(
            requestedWidthPixels,
            PlannerContextualIdeasRail.MinimumWidthPixels,
            PlannerContextualIdeasRail.MaximumWidthPixels);
        return Task.CompletedTask;
    }

    private bool IsAuthenticated =>
        HttpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated is true;

    private string CurrentLocalPath => InitialPath;

    private string DevelopmentSignInPath =>
        $"{ExternalIdBrowserEndpoints.SignInPath}?returnUrl={Uri.EscapeDataString(CurrentLocalPath)}";

    private bool IsDevelopmentAuthentication =>
        HttpContextAccessor.HttpContext?.RequestServices
            .GetService<AuthenticationConfiguration>()?.Mode == AuthenticationMode.Development;

    protected override async Task OnInitializedAsync()
    {
        var context = HttpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        if (!TryGetAddressedRoute(InitialPath, out var creatorId, out var planId))
        {
            LoadState = WorkspaceLoadState.Landing;
            return;
        }

        LoadState = WorkspaceLoadState.Loading;
        var actorResolver = context.RequestServices.GetService<IWorkspaceActorResolver>();
        var query = context.RequestServices.GetService<IPlannerWorkspaceQueryService>();
        var actor = actorResolver?.Resolve(context.User);
        if (query is null || actor is null)
        {
            LoadState = WorkspaceLoadState.Unavailable;
            return;
        }

        try
        {
            AddressedCreatorId = creatorId;
            if (planId.HasValue)
            {
                var result = await query.GetAsync(actor, creatorId, planId.Value, context.RequestAborted);
                Plan = result.Plan;
                PlanCanEdit = result.CanEdit;
                LoadState = result.IsAllowed ? WorkspaceLoadState.Ready : WorkspaceLoadState.Unavailable;
            }
            else
            {
                var result = await query.ListAsync(actor, creatorId, context.RequestAborted);
                Plans = result.Plans;
                LoadState = result.IsAllowed ? WorkspaceLoadState.Ready : WorkspaceLoadState.Unavailable;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            context.RequestServices.GetService<ILogger<WorkspaceRoot>>()?.LogError(
                exception,
                "Planner dashboard read failed before a safe response could be rendered.");
            LoadState = WorkspaceLoadState.Failure;
        }
    }

    private static bool TryGetAddressedRoute(
        PathString path,
        out CreatorId creatorId,
        out AdventurePlanId? planId)
    {
        creatorId = default;
        planId = null;
        var segments = path.Value?.Split('/', StringSplitOptions.None);
        if (segments is null
            || segments.Length is not (5 or 6)
            || segments[0] != string.Empty
            || segments[1] != "workspace"
            || segments[2] != "creators"
            || segments[4] != "plans")
        {
            return false;
        }

        try
        {
            creatorId = new CreatorId(segments[3]);
            if (segments.Length == 6)
            {
                planId = new AdventurePlanId(segments[5]);
            }
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private string PlanListPath => $"/workspace/creators/{AddressedCreatorId.Value}/plans";
    private string CreatePlanPath => $"{PlanListPath}/create";
    private string EditPlanPath => $"{PlanListPath}/{Plan!.Id.Value}/overview";
    private string AddDestinationPath => $"{PlanListPath}/{Plan!.Id.Value}/destinations";
    private string AddDayPath => $"{PlanListPath}/{Plan!.Id.Value}/days";
    private string AddActivityPath => $"{PlanListPath}/{Plan!.Id.Value}/activities";
    private string AddTransportationPath => $"{PlanListPath}/{Plan!.Id.Value}/transportation";
    private string AddAccommodationPath => $"{PlanListPath}/{Plan!.Id.Value}/accommodations";
    private string AddReservationPath => $"{PlanListPath}/{Plan!.Id.Value}/reservations";
    private string PlanPath(AdventurePlanId planId) => $"{PlanListPath}/{planId.Value}";
    private string GetQueryValue(string name) =>
        QueryHelpers.ParseQuery(InitialQueryString).TryGetValue(name, out var value)
            ? value.ToString()
            : string.Empty;
    private string? CreateStatusMessage =>
        GetQueryValue("create") switch
        {
            "denied" => "The plan could not be created for this workspace.",
            "conflict" => "This request no longer matches its original submission. Start a new request.",
            "validation" => "Review the plan details and try again.",
            "failure" => "The plan could not be created. Please try again.",
            _ => null
        };
    private string? EditStatusMessage =>
        GetQueryValue("edit") switch
        {
            "updated" => "The plan overview was updated.",
            "unchanged" => "The plan overview was already current.",
            "denied" => "The plan overview could not be updated.",
            "conflict" => "This plan changed. Review the current values and try again.",
            "validation" => "Review the overview fields and try again.",
            "date-blocked" => "Plan dates cannot change while dated itinerary records exist.",
            "failure" => "The plan overview could not be updated. Please try again.",
            _ => null
        };
    private bool IsEditConflict =>
        GetQueryValue("edit") == "conflict";
    private string? DestinationStatusMessage =>
        GetQueryValue("destination") switch
        {
            "added" => "The destination was added to the route.",
            "denied" => "The destination could not be added.",
            "conflict" => "This plan changed. Review the current route and try again.",
            "validation" => "Review the destination name, dates, and IANA time zone.",
            "failure" => "The destination could not be added. Please try again.",
            _ => null
        };
    private string? DayStatusMessage =>
        GetQueryValue("day") switch
        {
            "added" => "The itinerary day was added.",
            "denied" => "The itinerary day could not be added.",
            "conflict" => "This plan changed. Review the current itinerary and try again.",
            "validation" => "Review the destination visit, local date, and day title.",
            "failure" => "The itinerary day could not be added. Please try again.",
            _ => null
        };
    private string? DayEditStatusMessage =>
        GetQueryValue("day-edit") switch
        {
            "updated" => "The itinerary day was updated.",
            "unchanged" => "The itinerary day was already current.",
            "denied" => "The itinerary day could not be updated.",
            "conflict" => "This plan changed. Review the current itinerary day and try again.",
            "validation" => "Review the itinerary-day title and try again.",
            "failure" => "The itinerary day could not be updated. Please try again.",
            _ => null
        };
    private string? ActivityStatusMessage =>
        GetQueryValue("activity") switch
        {
            "added" => "The proposed activity was added.",
            "denied" => "The activity could not be added.",
            "conflict" => "This plan changed. Review the current itinerary and try again.",
            "validation" => "Review the itinerary day, activity title, and local times.",
            "failure" => "The activity could not be added. Please try again.",
            _ => null
        };
    private string? ActivityEditStatusMessage =>
        GetQueryValue("activity-edit") switch
        {
            "updated" => "The activity was updated.",
            "unchanged" => "The activity was already current.",
            "denied" => "The activity could not be updated.",
            "conflict" => "This plan changed. Review the current activity values and try again.",
            "validation" => "Review the activity title and local times.",
            "failure" => "The activity could not be updated. Please try again.",
            _ => null
        };
    private string? TransportationStatusMessage =>
        GetQueryValue("transportation") switch
        {
            "added" => "The proposed transportation was added.",
            "denied" => "The transportation could not be added.",
            "conflict" => "This plan changed. Review transportation and try again.",
            "validation" => "Review the route, local dates, times, and IANA time zones.",
            "failure" => "The transportation could not be added. Please try again.",
            _ => null
        };
    private string? TransportationEditStatusMessage =>
        GetQueryValue("transportation-edit") switch
        {
            "updated" => "The transportation segment was updated.",
            "unchanged" => "The transportation segment was already current.",
            "denied" => "The transportation segment could not be updated.",
            "conflict" => "This plan changed. Review the current transportation values and try again.",
            "validation" => "Review the route, local dates, times, and IANA time zones.",
            "failure" => "The transportation segment could not be updated. Please try again.",
            _ => null
        };
    private string? AccommodationStatusMessage =>
        GetQueryValue("accommodation") switch
        {
            "added" => "The proposed accommodation was added.",
            "denied" => "The accommodation could not be added.",
            "conflict" => "This plan changed. Review accommodations and try again.",
            "validation" => "Review the accommodation name, dates, and IANA time zone.",
            "failure" => "The accommodation could not be added. Please try again.",
            _ => null
        };
    private string? AccommodationEditStatusMessage =>
        GetQueryValue("accommodation-edit") switch
        {
            "updated" => "The accommodation was updated.",
            "unchanged" => "The accommodation was already current.",
            "denied" => "The accommodation could not be updated.",
            "conflict" => "This plan changed. Review the current accommodation values and try again.",
            "validation" => "Review the accommodation name, inclusive dates, and IANA time zone.",
            "failure" => "The accommodation could not be updated. Please try again.",
            _ => null
        };
    private string? ReservationStatusMessage =>
        GetQueryValue("reservation") switch
        {
            "added" => "The proposed reservation was added.",
            "denied" => "The reservation could not be added.",
            "conflict" => "This plan changed. Review reservations and try again.",
            "validation" => "Review the reservation subject and try again.",
            "failure" => "The reservation could not be added. Please try again.",
            _ => null
        };
    private static string FormatDates(AdventurePlanDashboardItem plan) => FormatDates(plan.Dates);
    private static string FormatDates(PlanningDateRange dates) =>
        $"{dates.Start:MMM d, yyyy} – {dates.End:MMM d, yyyy}";

    private static string FormatStatus(PlanningStatus status) => status switch
    {
        PlanningStatus.InProgress => "In progress",
        _ => status.ToString()
    };

    private enum WorkspaceLoadState { Landing, Loading, Ready, Unavailable, Failure }

}
