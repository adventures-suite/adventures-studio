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
        Assert.Equal(6, Count(html, "name=\"expectedVersion\" value=\"17\""));
        Assert.Contains("name=\"itineraryDayId\" value=\"day_madrid_01\"", html);
        Assert.Contains("name=\"destinationVisitId\"", html);
        Assert.Contains("name=\"departureTimeZoneId\"", html);
        Assert.Contains("name=\"arrivalTimeZoneId\"", html);
        Assert.Contains("name=\"subject\" required", html);
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
        Assert.Contains("Transportation status", html);
        Assert.Contains("Accommodation status", html);
        Assert.Contains("Reservation status", html);
        Assert.Equal(6, Count(html, "role=\"status\""));
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
                [nameof(PlannerItineraryBoard.AddTransportationPath)] = Paths["transportation"],
                [nameof(PlannerItineraryBoard.AddAccommodationPath)] = Paths["accommodation"],
                [nameof(PlannerItineraryBoard.AddReservationPath)] = Paths["reservation"]
            };
            if (includeMessages)
            {
                parameters[nameof(PlannerItineraryBoard.DestinationStatusMessage)] = "Destination status";
                parameters[nameof(PlannerItineraryBoard.DayStatusMessage)] = "Day status";
                parameters[nameof(PlannerItineraryBoard.ActivityStatusMessage)] = "Activity status";
                parameters[nameof(PlannerItineraryBoard.TransportationStatusMessage)] = "Transportation status";
                parameters[nameof(PlannerItineraryBoard.AccommodationStatusMessage)] = "Accommodation status";
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
}
