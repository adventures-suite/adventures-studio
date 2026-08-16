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
    /// <summary>The board renders every allowlisted record with semantic order and no credentials.</summary>
    [Fact]
    public async Task Board_RendersAuthorizedProjectionWithoutSensitiveValues()
    {
        var html = await RenderAsync(canEdit: false);

        Assert.Contains("Plan the journey", html);
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
        Assert.Equal(9, Count(html, "method=\"post\""));
        Assert.Equal(9, Count(html, "name=\"expectedVersion\" value=\"17\""));
        Assert.Equal(9, Count(html, "<details"));
        Assert.Equal(9, Count(html, "name=\"planner-board-action\""));
        Assert.DoesNotContain("name=\"activity-editor\"", html);
        Assert.DoesNotContain("name=\"transportation-editor\"", html);
        Assert.DoesNotContain("name=\"accommodation-editor\"", html);
        Assert.DoesNotContain("<details open", html);
        Assert.DoesNotContain("aria-expanded", html);
        Assert.Contains(">Add destination</summary>", html);
        Assert.Contains(">Add itinerary day</summary>", html);
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

    /// <summary>Allowlisted PRG messages remain scoped to their board sections without reflecting arbitrary input.</summary>
    [Fact]
    public async Task Board_RendersOnlyPassedAllowlistedStatusMessages()
    {
        var html = await RenderAsync(canEdit: false, includeMessages: true);

        Assert.Contains("Destination status", html);
        Assert.Contains("Day status", html);
        Assert.Contains("Activity status", html);
        Assert.Contains("Activity edit status", html);
        Assert.Contains("Transportation status", html);
        Assert.Contains("Transportation edit status", html);
        Assert.Contains("Accommodation status", html);
        Assert.Contains("Accommodation edit status", html);
        Assert.Contains("Reservation status", html);
        Assert.Equal(9, Count(html, "role=\"status\""));
        Assert.DoesNotContain("PRIVATE-QUERY-VALUE", html, StringComparison.Ordinal);
    }

    /// <summary>Board regions, articles, ordered lists, dates, and status text remain machine-readable.</summary>
    [Fact]
    public async Task Board_RendersAccessibleDocumentStructure()
    {
        var html = await RenderAsync(canEdit: false);

        Assert.Contains("aria-labelledby=\"planner-board-heading\"", html);
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

    /// <summary>PRG status rendering never opens an ambiguous creation or edit disclosure.</summary>
    [Fact]
    public async Task Board_PrgMessages_LeaveEveryDisclosureClosed()
    {
        var html = await RenderAsync(canEdit: true, includeMessages: true);

        Assert.Equal(9, Count(html, "role=\"status\""));
        Assert.Equal(9, Count(html, "name=\"planner-board-action\""));
        Assert.DoesNotContain("<details open", html);
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

    private static async Task<string> RenderAsync(bool canEdit, bool includeMessages = false, AdventurePlanDetail? plan = null)
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
            if (includeMessages)
            {
                parameters[nameof(PlannerItineraryBoard.DestinationStatusMessage)] = "Destination status";
                parameters[nameof(PlannerItineraryBoard.DayStatusMessage)] = "Day status";
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
