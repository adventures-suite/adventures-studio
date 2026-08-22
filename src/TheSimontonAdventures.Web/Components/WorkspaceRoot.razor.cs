using AdventuresSuite.Identity;
using AdventuresSuite.Identity.ExternalId;
using AdventuresSuite.Planning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
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

    [Inject]
    private WorkspaceNavigationConfiguration WorkspaceNavigation { get; set; } = null!;

    [Inject]
    private IOptions<PlatformHostOptions> PlatformHostConfiguration { get; set; } = null!;

    private IReadOnlyList<AdventurePlanDashboardItem> Plans { get; set; } = [];
    private IReadOnlyList<CreatorWorkspaceChoice> AuthorizedCreatorWorkspaces { get; set; } = [];
    private AdventurePlanDetail? Plan { get; set; }
    private bool PlanCanEdit { get; set; }
    private CreatorId AddressedCreatorId { get; set; }
    private WorkspaceLoadState LoadState { get; set; } = WorkspaceLoadState.Landing;
    private string CreateIdempotencyKey { get; } = $"request_{Guid.NewGuid():N}";
    private PlannerIdeasContext? SelectedIdeasContext { get; set; }
    private IReadOnlyList<AdventureTemplateBlueprint> AdventureTemplates { get; set; } = [];
    private IReadOnlyList<PlannerFootStepDefinition> AuthorizedFootSteps { get; set; } = [];
    private ActorIdentity? WorkspaceActor { get; set; }
    private WorkspaceApplicationDefinition? PlaceholderApplication { get; set; }
    private bool IsTemplateMode { get; set; }
    private bool IsRootAdventureLanding { get; set; }
    private int IdeasWidthPixels { get; set; } = 320;
    private PlannerFootStepDefinition? DraggedDestinationFootStep { get; set; }
    private PlannerFootStepDefinition? PendingDestinationFootStep { get; set; }
    private PlannerFootStepDefinition? DraggedActivityFootStep { get; set; }
    private PlannerActivityFootStepDrop? PendingActivityFootStep { get; set; }
    private PlannerWorkspacePanel FocusedPanel { get; set; } = PlannerWorkspacePanel.Overview;
    private HashSet<PlannerWorkspacePanel> ExpandedPanels { get; } = [PlannerWorkspacePanel.Overview];

    private async Task FocusPanelAsync(PlannerWorkspacePanel panel)
    {
        FocusedPanel = panel;
        ExpandedPanels.Clear();
        ExpandedPanels.Add(panel == PlannerWorkspacePanel.Activities
            ? PlannerWorkspacePanel.Itinerary
            : panel);

        if (panel != PlannerWorkspacePanel.Activities || Plan is null || Plan.Days.Count == 0)
        {
            return;
        }

        var target = SelectedIdeasContext?.Kind switch
        {
            PlannerIdeasContextKind.Day => Plan.Days.FirstOrDefault(day =>
                string.Equals(day.Id.Value, SelectedIdeasContext.Id, StringComparison.Ordinal)),
            PlannerIdeasContextKind.Destination => Plan.Days
                .Where(day => string.Equals(day.DestinationVisitId?.Value,
                    SelectedIdeasContext.Id, StringComparison.Ordinal))
                .OrderByDescending(day => day.Activities.Count > 0)
                .ThenBy(day => day.Date)
                .FirstOrDefault(),
            _ => null
        };
        target ??= Plan.Days.OrderByDescending(day => day.Activities.Count > 0)
            .ThenBy(day => day.Date)
            .First();
        await SelectIdeasContextAsync(new PlannerIdeasContext(
            PlannerIdeasContextKind.Day, target.Id.Value, target.Title));
    }

    private Task TogglePanelAsync(PlannerWorkspacePanel panel)
    {
        if (!ExpandedPanels.Add(panel))
        {
            ExpandedPanels.Remove(panel);
        }

        return Task.CompletedTask;
    }

    private bool IsPanelExpanded(PlannerWorkspacePanel panel) => ExpandedPanels.Contains(panel);
    private bool IsPanelFocused(PlannerWorkspacePanel panel) => FocusedPanel == panel;
    private string OverviewPanelClasses => $"plan-create planner-panel{(IsPanelFocused(PlannerWorkspacePanel.Overview) ? " planner-panel--focused" : IsPanelExpanded(PlannerWorkspacePanel.Overview) ? " planner-panel--open" : " planner-panel--collapsed")}";
    private IReadOnlyList<PlannerActivityTarget> ActivityFootStepTargets
    {
        get
        {
            if (Plan is null || SelectedIdeasContext is null)
            {
                return [];
            }

            var days = SelectedIdeasContext.Kind switch
            {
                PlannerIdeasContextKind.Day => Plan.Days.Where(day =>
                    string.Equals(day.Id.Value, SelectedIdeasContext.Id, StringComparison.Ordinal)),
                PlannerIdeasContextKind.Destination => Plan.Days.Where(day =>
                    string.Equals(day.DestinationVisitId?.Value, SelectedIdeasContext.Id, StringComparison.Ordinal)),
                _ => []
            };

            return days.OrderBy(day => day.Date)
                .Select(day => new PlannerActivityTarget(
                    day.Id.Value, $"{day.Date:MMM d} · {day.Title}"))
                .ToArray();
        }
    }

    private Task BeginDestinationFootStepDragAsync(PlannerFootStepDefinition footStep)
    {
        DraggedDestinationFootStep = footStep.DestinationDraft is null ? null : footStep;
        return Task.CompletedTask;
    }

    private Task EndDestinationFootStepDragAsync()
    {
        DraggedDestinationFootStep = null;
        return Task.CompletedTask;
    }

    private Task DropDestinationFootStepAsync()
    {
        if (DraggedDestinationFootStep?.DestinationDraft is not null)
        {
            PendingDestinationFootStep = DraggedDestinationFootStep;
        }
        DraggedDestinationFootStep = null;
        return Task.CompletedTask;
    }

    private Task ReviewDroppedDestinationFootStepAsync(PlannerFootStepDefinition footStep)
    {
        if (footStep.DestinationDraft is not null)
        {
            PendingDestinationFootStep = footStep;
        }
        DraggedDestinationFootStep = null;
        return Task.CompletedTask;
    }

    private Task CancelDestinationFootStepReviewAsync()
    {
        PendingDestinationFootStep = null;
        return Task.CompletedTask;
    }

    private Task BeginActivityFootStepDragAsync(PlannerFootStepDefinition footStep)
    {
        DraggedActivityFootStep = footStep.ActivityDraft is null ? null : footStep;
        return Task.CompletedTask;
    }

    private Task EndActivityFootStepDragAsync()
    {
        DraggedActivityFootStep = null;
        return Task.CompletedTask;
    }

    private Task ReviewDroppedActivityFootStepAsync(PlannerActivityFootStepDrop drop)
    {
        if (DraggedActivityFootStep?.Id == drop.FootStep.Id
            && ActivityFootStepTargets.Any(target => target.Id == drop.Target.Id))
        {
            PendingActivityFootStep = drop;
        }
        DraggedActivityFootStep = null;
        return Task.CompletedTask;
    }

    private Task CancelActivityFootStepReviewAsync()
    {
        PendingActivityFootStep = null;
        return Task.CompletedTask;
    }

    private Task SetTemplateModeAsync(bool isTemplateMode)
    {
        IsTemplateMode = isTemplateMode;
        return Task.CompletedTask;
    }

    private async Task SelectIdeasContextAsync(PlannerIdeasContext context)
    {
        SelectedIdeasContext = context;
        await LoadFootStepsAsync(context);
    }

    private async Task SelectAdventureIdeasContextAsync()
    {
        if (Plan is not null)
        {
            SelectedIdeasContext = new PlannerIdeasContext(
                PlannerIdeasContextKind.Adventure,
                Plan.Id.Value,
                Plan.Title);
        }

        if (SelectedIdeasContext is not null)
        {
            await LoadFootStepsAsync(SelectedIdeasContext);
        }
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

    private string SignOutReturnPath =>
        LoadState == WorkspaceLoadState.Ready ? CurrentLocalPath : "/";

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

        LoadState = WorkspaceLoadState.Loading;
        var actorResolver = context.RequestServices.GetService<IWorkspaceActorResolver>();
        var query = context.RequestServices.GetService<IPlannerWorkspaceQueryService>();
        var actor = actorResolver?.Resolve(context.User);
        if (actor is null)
        {
            LoadState = WorkspaceLoadState.Unavailable;
            return;
        }

        WorkspaceActor = actor;

        if (TryGetWorkspaceApplicationRoute(
            InitialPath,
            out var applicationCreatorId,
            out var application))
        {
            var directory = context.RequestServices.GetService<ICreatorWorkspaceDirectoryService>();
            if (directory is null)
            {
                LoadState = WorkspaceLoadState.Unavailable;
                return;
            }

            try
            {
                AuthorizedCreatorWorkspaces = await directory.ListAsync(actor, context.RequestAborted);
                if (!AuthorizedCreatorWorkspaces.Any(workspace =>
                    workspace.CreatorId == applicationCreatorId))
                {
                    LoadState = WorkspaceLoadState.Unavailable;
                    return;
                }

                AddressedCreatorId = applicationCreatorId;
                PlaceholderApplication = application;
                LoadState = WorkspaceLoadState.Ready;
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                context.RequestServices.GetService<ILogger<WorkspaceRoot>>()?.LogError(
                    exception,
                    "Workspace application preview authorization failed before a safe response could be rendered.");
                LoadState = WorkspaceLoadState.Failure;
            }

            return;
        }

        if (!TryGetAddressedRoute(InitialPath, out var creatorId, out var planId))
        {
            var directory = context.RequestServices.GetService<ICreatorWorkspaceDirectoryService>();
            if (directory is null)
            {
                LoadState = WorkspaceLoadState.Landing;
                return;
            }

            try
            {
                AuthorizedCreatorWorkspaces = await directory.ListAsync(actor, context.RequestAborted);
                if (AuthorizedCreatorWorkspaces.Count == 1
                    && WorkspaceApplicationCatalog.TryGet("dream", out var dreamApplication)
                    && dreamApplication is not null)
                {
                    AddressedCreatorId = AuthorizedCreatorWorkspaces[0].CreatorId;
                    PlaceholderApplication = dreamApplication;
                    LoadState = WorkspaceLoadState.Ready;
                }
                else
                {
                    LoadState = WorkspaceLoadState.Landing;
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
                    "Creator workspace directory failed before a safe response could be rendered.");
                LoadState = WorkspaceLoadState.Failure;
            }
            return;
        }

        if (query is null)
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
                if (result.IsAllowed && Plan is not null)
                {
                    await SelectAdventureIdeasContextAsync();
                }
            }
            else
            {
                var result = await query.ListAsync(actor, creatorId, context.RequestAborted);
                Plans = result.Plans;
                LoadState = result.IsAllowed ? WorkspaceLoadState.Ready : WorkspaceLoadState.Unavailable;
                if (result.IsAllowed)
                {
                    var catalog = context.RequestServices
                        .GetService<IAdventureTemplateCatalogQueryService>();
                    if (catalog is not null)
                    {
                        try
                        {
                            var catalogResult = await catalog.ListAsync(
                                actor, creatorId, "en-US", context.RequestAborted);
                            AdventureTemplates = catalogResult.IsAllowed ? catalogResult.Templates : [];
                        }
                        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            context.RequestServices.GetService<ILogger<WorkspaceRoot>>()?.LogError(
                                exception,
                                "Planner Journey Template catalog failed; manual planning remains available.");
                            AdventureTemplates = [];
                        }
                    }
                }
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

    private async Task LoadFootStepsAsync(PlannerIdeasContext context)
    {
        if (WorkspaceActor is null || Plan is null)
        {
            AuthorizedFootSteps = [];
            return;
        }

        var service = HttpContextAccessor.HttpContext?.RequestServices.GetService<IPlannerFootStepQueryService>();
        if (service is null)
        {
            AuthorizedFootSteps = [];
            return;
        }

        var kind = context.Kind switch
        {
            PlannerIdeasContextKind.Adventure => PlannerFootStepContextKind.Adventure,
            PlannerIdeasContextKind.Destination => PlannerFootStepContextKind.Destination,
            PlannerIdeasContextKind.Day => PlannerFootStepContextKind.Day,
            _ => throw new InvalidOperationException("Unsupported Planner FootStep context.")
        };
        try
        {
            const int queryPageSize = 64;
            const int maximumCatalogPages = 16;
            var authorized = new List<PlannerFootStepDefinition>();
            for (var page = 1; page <= maximumCatalogPages; page++)
            {
                var result = await service.QueryAsync(new PlannerFootStepQuery(
                    WorkspaceActor, AddressedCreatorId, Plan.Id, kind, context.Id, "en-US",
                    new PlannerFootStepFilters(), page, queryPageSize));
                if (!result.IsAllowed)
                {
                    AuthorizedFootSteps = [];
                    return;
                }

                authorized.AddRange(result.Items);
                if (authorized.Count >= result.TotalItems || result.Items.Count == 0)
                {
                    break;
                }
            }

            AuthorizedFootSteps = authorized;
        }
        catch (OperationCanceledException) when (
            HttpContextAccessor.HttpContext?.RequestAborted.IsCancellationRequested is true)
        {
            throw;
        }
        catch (Exception exception)
        {
            HttpContextAccessor.HttpContext?.RequestServices.GetService<ILogger<WorkspaceRoot>>()?.LogError(
                exception, "Planner FootStep catalog failed; manual planning remains available.");
            AuthorizedFootSteps = [];
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

    private static bool TryGetWorkspaceApplicationRoute(
        PathString path,
        out CreatorId creatorId,
        out WorkspaceApplicationDefinition application)
    {
        creatorId = default;
        application = null!;
        var segments = path.Value?.Split('/', StringSplitOptions.None);
        if (segments is null
            || segments.Length != 5
            || segments[0] != string.Empty
            || segments[1] != "workspace"
            || segments[2] != "creators"
            || !WorkspaceApplicationCatalog.TryGet(segments[4], out var resolvedApplication)
            || resolvedApplication is null)
        {
            return false;
        }

        application = resolvedApplication;

        try
        {
            creatorId = new CreatorId(segments[3]);
            return true;
        }
        catch (ArgumentException)
        {
            application = null!;
            return false;
        }
    }

    private string PlanListPath => $"/workspace/creators/{AddressedCreatorId.Value}/plans";
    private string? WorkspaceBasePath => LoadState != WorkspaceLoadState.Ready
        || string.IsNullOrWhiteSpace(AddressedCreatorId.Value)
        ? null
        : $"/workspace/creators/{AddressedCreatorId.Value}";
    private string ActiveApplicationSlug => PlaceholderApplication?.Slug ?? "planner";
    private string WorkspaceTitle => PlaceholderApplication?.Name ?? "Planner";
    private string WorkspaceDescription => PlaceholderApplication is null
        ? "Adventure planning made simple."
        : "One connected experience for every stage of your Adventure.";
    private string PlannerHeroImageUrl => PlatformHostConfiguration.Value.JourneyImageUrl;
    private string CreatePlanPath => $"{PlanListPath}/create";
    private string CreateFromTemplatePath => $"{PlanListPath}/create-from-template";
    private string EditPlanPath => $"{PlanListPath}/{Plan!.Id.Value}/overview";
    private string AddDestinationPath => $"{PlanListPath}/{Plan!.Id.Value}/destinations";
    private string ReorderDestinationPath => $"{AddDestinationPath}/reorder";
    private string ApplyDestinationFootStepPath => $"{PlanListPath}/{Plan!.Id.Value}/footsteps/destination";
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
    private string? TemplateStatusMessage =>
        GetQueryValue("template") switch
        {
            "denied" => "That Journey Template is not available to this workspace.",
            "conflict" => "This template request no longer matches its original submission. Start a new request.",
            "validation" => "Review the Journey Template and start date, then try again.",
            "failure" => "The private Journey could not be created. Please try again.",
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
    private string? RouteStatusMessage =>
        GetQueryValue("route") switch
        {
            "reordered" => "The route and linked dates were updated.",
            "unchanged" => "The destination is already in that position.",
            "booking-locked" => "This route cannot be reordered automatically because booked or committed items would be affected.",
            "schedule-conflict" => "This route order conflicts with an existing transportation schedule. Adjust or remove the proposed transportation segment, then try again.",
            "denied" => "The route could not be reordered.",
            "conflict" => "This plan changed. Review the current route and try again.",
            "validation" => "Review the destination order and try again.",
            "failure" => "The route could not be reordered. Your plan was not changed.",
            _ => null
        };
    private string? FootStepStatusMessage =>
        GetQueryValue("footstep") switch
        {
            "added" => "The Destination FootStep was added to the plan with its source recorded.",
            "denied" => "That FootStep is not available to this workspace.",
            "conflict" => "This plan changed. Review the FootStep and current plan before trying again.",
            "validation" => "Review the destination dates and try again.",
            "failure" => "The FootStep could not be added. Your plan was not changed.",
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
