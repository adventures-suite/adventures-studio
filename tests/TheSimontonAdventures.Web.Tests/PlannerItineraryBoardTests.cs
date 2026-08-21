using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AdventuresSuite.Planning;
using TheSimontonAdventures.Web.Components;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the authorized Planner itinerary-board presentation boundary.</summary>
public sealed class PlannerItineraryBoardTests
{
    [Fact]
    public async Task Board_ItineraryDay_RendersAccessibleCalendarThumbnail()
    {
        var output = await RenderAsync(canEdit: false);

        Assert.Contains("October 2027 calendar, October 25 selected", output, StringComparison.Ordinal);
        Assert.Contains("planner-calendar__selected", output, StringComparison.Ordinal);
        Assert.Contains(">25</span>", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Board_TransportationAndAccommodation_RenderCalendarRanges()
    {
        var output = await RenderAsync(canEdit: true);

        Assert.Contains("Calendar, October 24, 2027 through October 25, 2027 selected", output, StringComparison.Ordinal);
        Assert.Contains("Calendar, October 25, 2027 through October 29, 2027 selected", output, StringComparison.Ordinal);
        Assert.True(Regex.Matches(output, "planner-calendar__selected").Count >= 8);
        Assert.Contains("aria-label=\"Edit transportation: Flight from Phoenix to Madrid\"", output, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Edit accommodation: Hotel Central\"", output, StringComparison.Ordinal);
    }

    /// <summary>The board renders every allowlisted record with semantic order and no credentials.</summary>
    [Fact]
    public async Task Board_RendersAuthorizedProjectionWithoutSensitiveValues()
    {
        var html = await RenderAsync(canEdit: false);

        Assert.Contains("aria-label=\"Journey planning board\"", html);
        Assert.DoesNotContain("Authorized plan details only", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("Madrid", StringComparison.Ordinal) < html.IndexOf("Barcelona", StringComparison.Ordinal));
        Assert.Contains("Arrival day", html);
        Assert.Contains("Prado Museum", html);
        Assert.Contains("Flight: Phoenix to Madrid", html);
        Assert.Contains("Hotel Central", html);
        Assert.Contains("Museum hold", html);
        Assert.Contains("Credential-free planning summaries only", html);
        Assert.Contains("does not prove a booking or confirmation", html);
        Assert.DoesNotContain("RESERVATION-SECRET-123", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Traveler Private Name", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
    }

    /// <summary>An editable board preserves every existing POST path, field, identity, and expected version.</summary>
    [Fact]
    public async Task Board_Editable_PreservesMutationFormContract()
    {
        var html = await RenderAsync(canEdit: true);

        foreach (var path in Paths.Values)
        {
            Assert.Contains($"action=\"{path}\"", html, StringComparison.Ordinal);
        }
        Assert.Equal(10, Count(html, "method=\"post\""));
        Assert.Equal(10, Count(html, "name=\"expectedVersion\" value=\"17\""));
        Assert.Equal(10, Count(html, "<details"));
        Assert.Equal(10, Count(html, "name=\"planner-board-action\""));
        Assert.DoesNotContain("name=\"activity-editor\"", html);
        Assert.DoesNotContain("name=\"transportation-editor\"", html);
        Assert.DoesNotContain("name=\"accommodation-editor\"", html);
        Assert.DoesNotContain("<details open", html);
        Assert.Equal(5, Count(html, "aria-controls=\"plan-"));
        Assert.Contains(">Add destination</summary>", html);
        Assert.Contains("draggable=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Move Madrid later\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Move Barcelona earlier\"", html, StringComparison.Ordinal);
        Assert.Contains(">Add itinerary day</summary>", html);
        Assert.Contains("Edit itinerary day: Arrival day", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/days/day_madrid_01/edit\"", html);
        Assert.Contains("name=\"title\" value=\"Arrival day\"", html);
        Assert.DoesNotContain("name=\"date\" value=\"2027-10-25\"", html);
        Assert.Contains(">Add activity to Arrival day</summary>", html);
        Assert.Contains(">Add transportation</summary>", html);
        Assert.Contains(">Add accommodation</summary>", html);
        Assert.Contains(">Add reservation summary</summary>", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/activities/activity_prado_01/edit\"", html);
        Assert.Contains("Edit activity: Prado Museum", html);
        Assert.Contains("href=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027\"", html);
        Assert.Contains(">Cancel</a>", html);
        Assert.Contains("name=\"title\" value=\"Prado Museum\"", html);
        Assert.Contains("name=\"itineraryDayId\" value=\"day_madrid_01\"", html);
        Assert.Contains("name=\"destinationVisitId\"", html);
        Assert.Contains("name=\"date\" min=\"2027-10-25\" max=\"2027-11-05\" required", html);
        Assert.Contains("name=\"title\" required maxlength=\"200\" autocomplete=\"off\" placeholder=\"Arrival in Rome\"", html);
        Assert.Contains("name=\"departureTimeZoneId\"", html);
        Assert.Contains("name=\"arrivalTimeZoneId\"", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/transportation/transport_phx_mad/edit\"", html);
        Assert.Contains("Edit transportation: Flight from Phoenix to Madrid", html);
        Assert.Contains("name=\"mode\" value=\"Flight\"", html);
        Assert.Contains("name=\"departureDate\" value=\"2027-10-24\"", html);
        Assert.Contains("name=\"departureTimeZoneId\" value=\"America/Phoenix\"", html);
        Assert.Contains("name=\"arrivalTimeZoneId\" value=\"Europe/Madrid\"", html);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/accommodations/stay_madrid_01/edit\"", html);
        Assert.Contains("Edit accommodation: Hotel Central", html);
        Assert.Contains("name=\"name\" value=\"Hotel Central\"", html);
        Assert.Contains("Start date", html);
        Assert.Contains("End date", html);
        Assert.DoesNotContain("Check-in", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Check-out", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"startDate\" value=\"2027-10-25\"", html);
        Assert.Contains("name=\"endDate\" value=\"2027-10-29\"", html);
        Assert.Contains("name=\"timeZoneId\" value=\"Europe/Madrid\"", html);
        Assert.Contains("name=\"subject\" required", html);
        Assert.Contains("name=\"subject\" required maxlength=\"200\" autocomplete=\"off\" placeholder=\"Hotel, tour, or transportation hold\"", html);
        Assert.Contains("Confirmation references are added through a separate protected workflow.", html);
    }

    /// <summary>Controlled panel state hides inactive content without removing headers or unsaved form controls.</summary>
    [Fact]
    public async Task Board_FocusedPanel_RendersOnlyItsExpandedContent()
    {
        var html = await RenderAsync(
            canEdit: false,
            expandedPanels: new HashSet<PlannerWorkspacePanel> { PlannerWorkspacePanel.Transportation });

        Assert.Contains("id=\"plan-route-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"plan-itinerary-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"plan-accommodations-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"plan-reservations-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"plan-transportation-content\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"plan-transportation-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("Flight: Phoenix to Madrid", html, StringComparison.Ordinal);
        Assert.Contains("Prado Museum", html, StringComparison.Ordinal);
        Assert.Contains("Hotel Central", html, StringComparison.Ordinal);
        Assert.Contains("Museum hold", html, StringComparison.Ordinal);
    }

    /// <summary>Activity focus keeps activities under their days while making that workflow explicit.</summary>
    [Fact]
    public async Task Board_ActivityFocus_EmphasizesActivitiesWithinItinerary()
    {
        var html = await RenderAsync(
            canEdit: true,
            expandedPanels: new HashSet<PlannerWorkspacePanel> { PlannerWorkspacePanel.Itinerary },
            focusedPanel: PlannerWorkspacePanel.Activities);

        Assert.Contains("Activities by day", html, StringComparison.Ordinal);
        Assert.Contains("Activity focus", html, StringComparison.Ordinal);
        Assert.Contains("Find Activity FootSteps for Arrival day", html, StringComparison.Ordinal);
        Assert.Contains("Prado Museum", html, StringComparison.Ordinal);
        Assert.Contains("Add activity to Arrival day", html, StringComparison.Ordinal);
        Assert.Contains("planner-board__activities-focus", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"plan-itinerary-content\" hidden", html, StringComparison.Ordinal);
        Assert.Contains("id=\"plan-route-content\" hidden", html, StringComparison.Ordinal);
    }

    /// <summary>Allowlisted PRG messages remain scoped to their board sections without reflecting arbitrary input.</summary>
    [Fact]
    public async Task Board_RendersOnlyPassedAllowlistedStatusMessages()
    {
        var html = await RenderAsync(canEdit: false, includeMessages: true);

        Assert.Contains("Destination status", html);
        Assert.Contains("Day status", html);
        Assert.Contains("Day edit status", html);
        Assert.Contains("Activity status", html);
        Assert.Contains("Activity edit status", html);
        Assert.Contains("Transportation status", html);
        Assert.Contains("Transportation edit status", html);
        Assert.Contains("Accommodation status", html);
        Assert.Contains("Accommodation edit status", html);
        Assert.Contains("Reservation status", html);
        Assert.Equal(10, Count(html, "role=\"status\""));
        Assert.DoesNotContain("PRIVATE-QUERY-VALUE", html, StringComparison.Ordinal);
    }

    /// <summary>Board regions, articles, ordered lists, dates, and status text remain machine-readable.</summary>
    [Fact]
    public async Task Board_RendersAccessibleDocumentStructure()
    {
        var html = await RenderAsync(canEdit: false);

        Assert.Contains("aria-label=\"Journey planning board\"", html);
        Assert.Contains("aria-labelledby=\"plan-route\"", html);
        Assert.Contains("aria-labelledby=\"plan-itinerary\"", html);
        Assert.Contains("aria-labelledby=\"plan-transportation\"", html);
        Assert.Contains("aria-labelledby=\"plan-accommodations\"", html);
        Assert.Contains("aria-labelledby=\"plan-reservations\"", html);
        Assert.Contains("<article", html);
        Assert.Contains("<ol", html);
        Assert.Contains("datetime=\"2027-10-25\"", html);
        Assert.Contains(">Proposed<", html);
    }

    /// <summary>Every visible form control has one visible, explicit, contextual label.</summary>
    [Fact]
    public async Task Board_Editable_AssociatesVisibleLabelsWithEveryControl()
    {
        var html = await RenderAsync(canEdit: true);
        var controls = Regex.Matches(
            html,
            "<(input|select|textarea)\\b(?<attributes>[^>]*)>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var controlIds = new List<string>();

        foreach (Match control in controls)
        {
            var attributes = control.Groups["attributes"].Value;
            if (Regex.IsMatch(attributes, "\\btype=\"hidden\"", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var idMatch = Regex.Match(attributes, "\\bid=\"(?<id>[^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(idMatch.Success, $"Visible control lacks an id: {control.Value}");
            var id = idMatch.Groups["id"].Value;
            controlIds.Add(id);

            var labelMatch = Regex.Match(
                html,
                $"<label\\b[^>]*\\bfor=\"{Regex.Escape(id)}\"[^>]*>(?<text>.*?)</label>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            Assert.True(labelMatch.Success, $"Control '{id}' lacks an explicit label association.");
            var labelText = Regex.Replace(labelMatch.Groups["text"].Value, "<[^>]+>", " ");
            Assert.False(string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(labelText)));
        }

        Assert.NotEmpty(controlIds);
        Assert.Equal(controlIds.Count, controlIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(">Day title for Oct 25</label>", html);
        Assert.Contains(">Activity title for Prado Museum</label>", html);
        Assert.Contains(">Mode for Phoenix to Madrid</label>", html);
        Assert.Contains(">Accommodation name for Hotel Central</label>", html);
    }

    /// <summary>All empty categories provide explicit, non-disclosing text.</summary>
    [Fact]
    public async Task Board_EmptyProjection_RendersEveryEmptyState()
    {
        var html = await RenderAsync(canEdit: false, plan: EmptyPlan());

        Assert.Contains("No destinations have been added.", html);
        Assert.Contains("No itinerary days have been added.", html);
        Assert.Contains("No transportation has been added.", html);
        Assert.Contains("No accommodations have been added.", html);
        Assert.Contains("No reservation summaries have been added.", html);
    }

    /// <summary>An editable empty plan exposes only immediately valid contextual actions.</summary>
    [Fact]
    public async Task Board_EmptyEditablePlan_PreservesDiscoverabilityAndDependencies()
    {
        var html = await RenderAsync(canEdit: true, plan: EmptyPlan());

        Assert.Contains(">Add destination</summary>", html);
        Assert.Contains(">Add transportation</summary>", html);
        Assert.Contains(">Add accommodation</summary>", html);
        Assert.Contains(">Add reservation summary</summary>", html);
        Assert.Contains("Add a destination before adding an itinerary day.", html);
        Assert.DoesNotContain(">Add itinerary day</summary>", html);
        Assert.DoesNotContain("Add activity to", html);
        Assert.Equal(4, Count(html, "name=\"planner-board-action\""));
        Assert.Equal(4, Count(html, "<form"));
        Assert.DoesNotContain("<details open", html);
    }

    /// <summary>Activity creation remains scoped to the authoritative enclosing itinerary day.</summary>
    [Fact]
    public async Task Board_MultipleDays_ContextsEachActivityCreationForm()
    {
        var plan = FullPlan() with
        {
            Days =
            [
                new(new("day_madrid_01"), new DestinationVisitId("visit_madrid_01"),
                    new(2027, 10, 25), new("Europe/Madrid"), "Arrival day", []),
                new(new("day_barcelona_01"), new DestinationVisitId("visit_barcelona_01"),
                    new(2027, 10, 30), new("Europe/Madrid"), "Barcelona day", [])
            ]
        };

        var html = await RenderAsync(canEdit: true, plan: plan);
        var madridArticle = Between(html, ">Arrival day</h5>", "</article>");
        var barcelonaArticle = Between(html, ">Barcelona day</h5>", "</article>");

        Assert.Contains(">Add activity to Arrival day</summary>", madridArticle);
        Assert.Contains("name=\"itineraryDayId\" value=\"day_madrid_01\"", madridArticle);
        Assert.Contains($"action=\"{Paths["activity"]}\"", madridArticle);
        Assert.Contains(">Add activity to Barcelona day</summary>", barcelonaArticle);
        Assert.Contains("name=\"itineraryDayId\" value=\"day_barcelona_01\"", barcelonaArticle);
        Assert.Contains($"action=\"{Paths["activity"]}\"", barcelonaArticle);
        Assert.Equal(2, Count(html, "Add activity to"));
    }

    /// <summary>Selecting a destination limits the Daily itinerary to days assigned to that destination.</summary>
    [Fact]
    public async Task Board_SelectedDestination_FiltersDailyItineraryAndOffersReset()
    {
        var context = new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "visit_madrid_01", "Madrid");

        var html = await RenderAsync(canEdit: false, plan: PlanWithMadridAndBarcelonaDays(), selectedContext: context);

        Assert.Contains("1 of 2 days", html, StringComparison.Ordinal);
        Assert.Contains("Showing itinerary for", html, StringComparison.Ordinal);
        Assert.Contains(">Madrid</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Show entire itinerary", html, StringComparison.Ordinal);
        Assert.Contains("Arrival day", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Barcelona day", html, StringComparison.Ordinal);
        Assert.Contains("1 of 2 segments", html, StringComparison.Ordinal);
        Assert.Contains("Phoenix", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Barcelona Sants", html, StringComparison.Ordinal);
        Assert.Contains("1 of 2 stays", html, StringComparison.Ordinal);
        Assert.Contains("Hotel Central", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Barcelona Harbor Hotel", html, StringComparison.Ordinal);
    }

    /// <summary>A stale destination selection cannot suppress authorized itinerary days.</summary>
    [Fact]
    public async Task Board_UnknownDestinationContext_FallsBackToEntireItinerary()
    {
        var context = new PlannerIdeasContext(PlannerIdeasContextKind.Destination, "visit_missing", "Missing");

        var html = await RenderAsync(canEdit: false, plan: PlanWithMadridAndBarcelonaDays(), selectedContext: context);

        Assert.Contains("2 days", html, StringComparison.Ordinal);
        Assert.Contains("Arrival day", html, StringComparison.Ordinal);
        Assert.Contains("Barcelona day", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Showing itinerary for", html, StringComparison.Ordinal);
    }

    /// <summary>PRG status rendering never opens an ambiguous creation or edit disclosure.</summary>
    [Fact]
    public async Task Board_PrgMessages_LeaveEveryDisclosureClosed()
    {
        var html = await RenderAsync(canEdit: true, includeMessages: true);

        Assert.Equal(10, Count(html, "role=\"status\""));
        Assert.Equal(10, Count(html, "name=\"planner-board-action\""));
        Assert.DoesNotContain("<details open", html);
    }

    /// <summary>A dropped destination FootStep opens review without bypassing the existing protected form.</summary>
    [Fact]
    public async Task Board_DroppedDestinationFootStep_RendersExplicitReviewBeforeMutation()
    {
        var html = await RenderAsync(canEdit: true, pendingFootStep: DestinationFootStep(), dragging: true);

        Assert.Contains("Drop the FootStep here to review it", html, StringComparison.Ordinal);
        Assert.Contains("Drop to review this destination", html, StringComparison.Ordinal);
        Assert.Contains("Your Journey will not change until you confirm.", html, StringComparison.Ordinal);
        Assert.Contains("data-planner-destination-drop=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("Add Barbados to this Journey?", html, StringComparison.Ordinal);
        Assert.Contains("This adds a proposed destination; it does not make a booking.", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/workspace/creators/creator_alpha_01/plans/plan_spain_2027/footsteps/destination\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"footStepId\" value=\"footstep_destination_barbados\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"expectedVersion\" value=\"17\"", html, StringComparison.Ordinal);
        Assert.Contains("Add destination to Journey", html, StringComparison.Ordinal);
        Assert.Contains(">Cancel</button>", html, StringComparison.Ordinal);
    }

    /// <summary>A dropped Activity FootStep targets one authorized day and still requires confirmation.</summary>
    [Fact]
    public async Task Board_DroppedActivityFootStep_RendersDayTargetAndReviewBeforeMutation()
    {
        var target = new PlannerActivityTarget("day_madrid_01", "Oct 25 · Arrival day");
        var html = await RenderAsync(canEdit: true, activityDragging: true,
            pendingActivity: new(ActivityFootStep(), target));

        Assert.Contains("data-planner-activity-drop=\"day_madrid_01\"", html, StringComparison.Ordinal);
        Assert.Contains("Drop here to review this activity for Arrival day", html, StringComparison.Ordinal);
        Assert.Contains("Use as an activity starting point", html, StringComparison.Ordinal);
        Assert.Contains("name=\"itineraryDayId\" value=\"day_madrid_01\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"title\" value=\"Evening tapas walk\"", html, StringComparison.Ordinal);
        Assert.Contains("Nothing is booked", html, StringComparison.Ordinal);
    }

    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["destination"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/destinations",
        ["day"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/days",
        ["activity"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/activities",
        ["transportation"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/transportation",
        ["accommodation"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/accommodations",
        ["reservation"] = "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/reservations"
    };

    private static async Task<string> RenderAsync(
        bool canEdit,
        bool includeMessages = false,
        AdventurePlanDetail? plan = null,
        PlannerFootStepDefinition? pendingFootStep = null,
        bool dragging = false,
        bool activityDragging = false,
        PlannerActivityFootStepDrop? pendingActivity = null,
        IReadOnlySet<PlannerWorkspacePanel>? expandedPanels = null,
        PlannerWorkspacePanel focusedPanel = PlannerWorkspacePanel.Transportation,
        PlannerIdeasContext? selectedContext = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery();
        services.AddHttpContextAccessor();
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = provider };
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(PlannerItineraryBoard.Plan)] = plan ?? FullPlan(),
                [nameof(PlannerItineraryBoard.CanEdit)] = canEdit,
                [nameof(PlannerItineraryBoard.AddDestinationPath)] = Paths["destination"],
                [nameof(PlannerItineraryBoard.AddDayPath)] = Paths["day"],
                [nameof(PlannerItineraryBoard.EditDayPathPrefix)] = Paths["day"],
                [nameof(PlannerItineraryBoard.DayCancelPath)] =
                    "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
                [nameof(PlannerItineraryBoard.AddActivityPath)] = Paths["activity"],
                [nameof(PlannerItineraryBoard.EditActivityPathPrefix)] = Paths["activity"],
                [nameof(PlannerItineraryBoard.ActivityCancelPath)] =
                    "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
                [nameof(PlannerItineraryBoard.AddTransportationPath)] = Paths["transportation"],
                [nameof(PlannerItineraryBoard.EditTransportationPathPrefix)] = Paths["transportation"],
                [nameof(PlannerItineraryBoard.TransportationCancelPath)] =
                    "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
                [nameof(PlannerItineraryBoard.AddAccommodationPath)] = Paths["accommodation"],
                [nameof(PlannerItineraryBoard.EditAccommodationPathPrefix)] = Paths["accommodation"],
                [nameof(PlannerItineraryBoard.AccommodationCancelPath)] =
                    "/workspace/creators/creator_alpha_01/plans/plan_spain_2027",
                [nameof(PlannerItineraryBoard.AddReservationPath)] = Paths["reservation"]
            };
            if (expandedPanels is not null)
            {
                parameters[nameof(PlannerItineraryBoard.ExpandedPanels)] = expandedPanels;
                parameters[nameof(PlannerItineraryBoard.FocusedPanel)] = focusedPanel;
            }
            parameters[nameof(PlannerItineraryBoard.IsDestinationFootStepDragging)] = dragging;
            parameters[nameof(PlannerItineraryBoard.IsActivityFootStepDragging)] = activityDragging;
            parameters[nameof(PlannerItineraryBoard.PendingActivityFootStep)] = pendingActivity;
            parameters[nameof(PlannerItineraryBoard.SelectedContext)] = selectedContext;
            parameters[nameof(PlannerItineraryBoard.PendingDestinationFootStep)] = pendingFootStep;
            parameters[nameof(PlannerItineraryBoard.ApplyDestinationFootStepPath)] =
                "/workspace/creators/creator_alpha_01/plans/plan_spain_2027/footsteps/destination";
            parameters[nameof(PlannerItineraryBoard.OnDestinationFootStepReviewCancelled)] =
                EventCallback.Factory.Create(new object(), () => { });
            if (includeMessages)
            {
                parameters[nameof(PlannerItineraryBoard.DestinationStatusMessage)] = "Destination status";
                parameters[nameof(PlannerItineraryBoard.DayStatusMessage)] = "Day status";
                parameters[nameof(PlannerItineraryBoard.DayEditStatusMessage)] = "Day edit status";
                parameters[nameof(PlannerItineraryBoard.ActivityStatusMessage)] = "Activity status";
                parameters[nameof(PlannerItineraryBoard.ActivityEditStatusMessage)] = "Activity edit status";
                parameters[nameof(PlannerItineraryBoard.TransportationStatusMessage)] = "Transportation status";
                parameters[nameof(PlannerItineraryBoard.TransportationEditStatusMessage)] =
                    "Transportation edit status";
                parameters[nameof(PlannerItineraryBoard.AccommodationStatusMessage)] = "Accommodation status";
                parameters[nameof(PlannerItineraryBoard.AccommodationEditStatusMessage)] =
                    "Accommodation edit status";
                parameters[nameof(PlannerItineraryBoard.ReservationStatusMessage)] = "Reservation status";
            }
            var output = await renderer.RenderComponentAsync<PlannerItineraryBoard>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private static PlannerFootStepDefinition DestinationFootStep() => new()
    {
        Id = "footstep_destination_barbados",
        Version = "1.0",
        Kind = "destination",
        Title = "Barbados island rhythm",
        Summary = "A balanced Eastern Caribbean stay.",
        Attribution = "AdventuresSuite curated test",
        Freshness = "Reviewed for testing",
        ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Adventure },
        DestinationDraft = new("Barbados", "America/Barbados"),
        DurationDays = 4
    };

    private static PlannerFootStepDefinition ActivityFootStep() => new()
    {
        Id = "footstep_activity_tapas_walk",
        Version = "1.0",
        Kind = "activity",
        Title = "Evening tapas walk",
        Summary = "A reviewable local activity starting point.",
        Attribution = "AdventuresSuite curated test",
        Freshness = "Reviewed for testing",
        ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Day },
        ActivityDraft = new("Evening tapas walk", new(18, 0), new(20, 0))
    };

    private static AdventurePlanDetail FullPlan() => new()
    {
        Id = new("plan_spain_2027"),
        Title = "Spain",
        LifecycleStage = AdventureLifecycleStage.Plan,
        Status = PlanningStatus.Draft,
        Dates = new(new(2027, 10, 25), new(2027, 11, 5)),
        Version = 17,
        TravelerCount = 2,
        Destinations =
        [
            new(new("visit_barcelona_01"), "Barcelona", new(new(2027, 10, 29), new(2027, 11, 5)), new("Europe/Madrid"), 2),
            new(new("visit_madrid_01"), "Madrid", new(new(2027, 10, 25), new(2027, 10, 29)), new("Europe/Madrid"), 1)
        ],
        Days = [new(new("day_madrid_01"), new DestinationVisitId("visit_madrid_01"), new(2027, 10, 25), new("Europe/Madrid"), "Arrival day", [new(new("activity_prado_01"), "Prado Museum", new(10, 0), new(12, 0), PlanItemStatus.Proposed)])],
        Transportation = [new(new("transport_phx_mad"), "Flight", "Phoenix", "Madrid", new(2027, 10, 24), new(18, 0), new("America/Phoenix"), new(2027, 10, 25), new(13, 0), new("Europe/Madrid"), PlanItemStatus.Proposed)],
        Accommodations = [new(new("stay_madrid_01"), "Hotel Central", new(new(2027, 10, 25), new(2027, 10, 29)), new("Europe/Madrid"), PlanItemStatus.Proposed)],
        Reservations = [new(new("reservation_museum_01"), "Museum hold", PlanItemStatus.Proposed)]
    };

    private static AdventurePlanDetail PlanWithMadridAndBarcelonaDays() => FullPlan() with
    {
        Days =
        [
            new(new("day_madrid_01"), new DestinationVisitId("visit_madrid_01"),
                new(2027, 10, 25), new("Europe/Madrid"), "Arrival day", []),
            new(new("day_barcelona_01"), new DestinationVisitId("visit_barcelona_01"),
                new(2027, 10, 30), new("Europe/Madrid"), "Barcelona day", [])
        ],
        Transportation =
        [
            new(new("transport_phx_mad"), "Flight", "Phoenix", "Madrid",
                new(2027, 10, 24), new(18, 0), new("America/Phoenix"),
                new(2027, 10, 25), new(13, 0), new("Europe/Madrid"),
                PlanItemStatus.Proposed, null, new("visit_madrid_01")),
            new(new("transport_mad_bar"), "Rail", "Madrid", "Barcelona Sants",
                new(2027, 10, 30), new(9, 0), new("Europe/Madrid"),
                new(2027, 10, 30), new(12, 0), new("Europe/Madrid"),
                PlanItemStatus.Proposed, new("visit_barcelona_01"), new("visit_barcelona_01"))
        ],
        Accommodations =
        [
            new(new("stay_madrid_01"), "Hotel Central",
                new(new(2027, 10, 25), new(2027, 10, 29)),
                new("Europe/Madrid"), PlanItemStatus.Proposed, new("visit_madrid_01")),
            new(new("stay_barcelona_01"), "Barcelona Harbor Hotel",
                new(new(2027, 10, 30), new(2027, 11, 5)),
                new("Europe/Madrid"), PlanItemStatus.Proposed, new("visit_barcelona_01"))
        ]
    };

    private static AdventurePlanDetail EmptyPlan() => new()
    {
        Id = new("plan_empty_01"),
        Title = "Empty",
        LifecycleStage = AdventureLifecycleStage.Plan,
        Status = PlanningStatus.Draft,
        Dates = new(new(2027, 10, 25), new(2027, 11, 5)),
        Version = 1,
        TravelerCount = 0
    };

    private static int Count(string value, string search) => value.Split(search, StringSplitOptions.None).Length - 1;

    private static string Between(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Expected start marker '{start}'.");
        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Expected end marker '{end}'.");
        return value[startIndex..endIndex];
    }
}
