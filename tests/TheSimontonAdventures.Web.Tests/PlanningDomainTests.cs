using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the private Creator-scoped Planning Engine domain foundation.</summary>
public sealed class PlanningDomainTests
{
    private static readonly CreatorId CreatorId = new("creator_tsa_01");
    private static readonly PlanningDateRange PlanDates = new(
        new DateOnly(2027, 10, 25),
        new DateOnly(2027, 11, 15));
    private static readonly PlanAudit Audit = new(
        1,
        new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 7, 20, 30, 0, TimeSpan.Zero));

    /// <summary>Ensures default aggregate and child identities cannot enter a plan.</summary>
    [Fact]
    public void Constructor_DefaultIdentity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AdventurePlan(
            default,
            CreatorId,
            "Plan",
            null,
            AdventureLifecycleStage.Plan,
            PlanningStatus.Draft,
            PlanDates,
            Audit));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            travelers: [new Traveler { Id = default, DisplayName = "Dianne" }]));
    }

    /// <summary>Ensures every plan has an explicit Creator ownership boundary.</summary>
    [Fact]
    public void Constructor_DefaultCreatorId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AdventurePlan(
            new AdventurePlanId("plan_spain_2027"),
            default,
            "Plan",
            null,
            AdventureLifecycleStage.Plan,
            PlanningStatus.Draft,
            PlanDates,
            Audit));
    }

    /// <summary>Ensures duplicate child identities cannot make edits ambiguous.</summary>
    [Fact]
    public void Constructor_DuplicateChildIdentity_ThrowsArgumentException()
    {
        var travelerId = new TravelerId("traveler_steve");

        Assert.Throws<ArgumentException>(() => CreatePlan(
            travelers:
            [
                new Traveler { Id = travelerId, DisplayName = "Steve" },
                new Traveler { Id = travelerId, DisplayName = "Dianne" }
            ]));
    }

    /// <summary>Ensures reversed travel ranges fail instead of being normalized silently.</summary>
    [Fact]
    public void PlanningDateRange_ReversedDates_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PlanningDateRange(
            new DateOnly(2027, 11, 15),
            new DateOnly(2027, 10, 25)));
    }

    /// <summary>Ensures children cannot extend outside the aggregate's date range.</summary>
    [Fact]
    public void Constructor_VisitOutsidePlanDates_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits:
            [
                new DestinationVisit
                {
                    Id = new DestinationVisitId("visit_madrid"),
                    Name = "Madrid",
                    Dates = new PlanningDateRange(new DateOnly(2027, 10, 24), new DateOnly(2027, 10, 26)),
                    TimeZone = new IanaTimeZone("Europe/Madrid"),
                    Sequence = 1
                }
            ]));
    }

    /// <summary>Ensures local activity times remain local and cannot be reversed.</summary>
    [Fact]
    public void Constructor_ReversedActivityTimes_ThrowsArgumentException()
    {
        var day = new ItineraryDay
        {
            Id = new ItineraryDayId("day_madrid_one"),
            Date = new DateOnly(2027, 10, 26),
            TimeZone = new IanaTimeZone("Europe/Madrid"),
            Title = "Madrid"
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            itineraryDays: [day],
            activities:
            [
                new PlannedActivity
                {
                    Id = new PlannedActivityId("activity_prado"),
                    ItineraryDayId = day.Id,
                    Title = "Prado Museum",
                    StartsAtLocal = new TimeOnly(15, 0),
                    EndsAtLocal = new TimeOnly(10, 0)
                }
            ]));
    }

    /// <summary>Ensures audit timestamps are UTC and distinct from travel dates.</summary>
    [Fact]
    public void PlanAudit_NonUtcTimestamp_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PlanAudit(
            1,
            new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.FromHours(-7)),
            new DateTimeOffset(2026, 8, 7, 20, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>Ensures the reference Spain and trans-Atlantic plan is representable.</summary>
    [Fact]
    public void Constructor_SpainTransAtlanticScenario_PreservesLocalAndPrivateState()
    {
        var madridVisit = new DestinationVisit
        {
            Id = new DestinationVisitId("visit_madrid"),
            Name = "Madrid",
            Dates = new PlanningDateRange(new DateOnly(2027, 10, 26), new DateOnly(2027, 10, 29)),
            TimeZone = new IanaTimeZone("Europe/Madrid"),
            Sequence = 1
        };
        var seaDay = new ItineraryDay
        {
            Id = new ItineraryDayId("day_atlantic_one"),
            Date = new DateOnly(2027, 11, 5),
            TimeZone = new IanaTimeZone("Atlantic/Canary"),
            Title = "Atlantic crossing"
        };

        var plan = CreatePlan(
            travelers:
            [
                new Traveler
                {
                    Id = new TravelerId("traveler_steve"),
                    DisplayName = "Steve",
                    Preferences = ["Photography", "Local food"]
                }
            ],
            destinationVisits: [madridVisit],
            itineraryDays: [seaDay],
            activities:
            [
                new PlannedActivity
                {
                    Id = new PlannedActivityId("activity_sea_day"),
                    ItineraryDayId = seaDay.Id,
                    Title = "Sea day photography review"
                }
            ],
            transportation:
            [
                new TransportationSegment
                {
                    Id = new TransportationSegmentId("transport_atlantic"),
                    Mode = "Cruise",
                    From = "Barcelona",
                    To = "Fort Lauderdale",
                    DepartureDate = new DateOnly(2027, 11, 1),
                    DepartureTimeZone = new IanaTimeZone("Europe/Madrid"),
                    ArrivalDate = new DateOnly(2027, 11, 15),
                    ArrivalTimeZone = new IanaTimeZone("America/New_York"),
                    Status = PlanItemStatus.Confirmed
                }
            ],
            accommodations:
            [
                new Accommodation
                {
                    Id = new AccommodationId("stay_madrid"),
                    Name = "Madrid hotel",
                    Dates = madridVisit.Dates,
                    TimeZone = madridVisit.TimeZone,
                    Status = PlanItemStatus.Reserved
                }
            ],
            reservations:
            [
                new Reservation
                {
                    Id = new ReservationId("reservation_cruise"),
                    Subject = "Trans-Atlantic cruise",
                    ConfirmationReference = "private-reference",
                    Status = PlanItemStatus.Confirmed
                }
            ],
            notes: [new PlanningNote { Id = new PlanningNoteId("note_private"), Text = "Private planning note" }],
            tasks: [new PlanningTask { Id = new PlanningTaskId("task_transfer"), Description = "Confirm port transfer" }],
            budgetItems:
            [
                new BudgetItem
                {
                    Id = new BudgetItemId("budget_cruise"),
                    Description = "Cruise fare",
                    Amount = 5000m,
                    CurrencyCode = "USD"
                }
            ],
            packingItems:
            [
                new PackingItem
                {
                    Id = new PackingItemId("packing_camera"),
                    Description = "Camera"
                }
            ]);

        Assert.Equal(CreatorId, plan.CreatorId);
        Assert.Equal(AdventureLifecycleStage.Plan, plan.LifecycleStage);
        Assert.Equal(PlanDates, plan.Dates);
        Assert.Equal("Europe/Madrid", plan.DestinationVisits[0].TimeZone.Value);
        Assert.Equal(new DateOnly(2027, 11, 5), plan.ItineraryDays[0].Date);
        Assert.Equal(TimeSpan.Zero, plan.Audit.UpdatedAtUtc.Offset);
        Assert.Equal("Private planning note", plan.Notes[0].Text);
    }

    private static AdventurePlan CreatePlan(
        IReadOnlyList<Traveler>? travelers = null,
        IReadOnlyList<DestinationVisit>? destinationVisits = null,
        IReadOnlyList<ItineraryDay>? itineraryDays = null,
        IReadOnlyList<PlannedActivity>? activities = null,
        IReadOnlyList<TransportationSegment>? transportation = null,
        IReadOnlyList<Accommodation>? accommodations = null,
        IReadOnlyList<Reservation>? reservations = null,
        IReadOnlyList<PlanningNote>? notes = null,
        IReadOnlyList<PlanningTask>? tasks = null,
        IReadOnlyList<BudgetItem>? budgetItems = null,
        IReadOnlyList<PackingItem>? packingItems = null)
    {
        return new AdventurePlan(
            new AdventurePlanId("plan_spain_2027"),
            CreatorId,
            "Spain and Trans-Atlantic Adventure",
            "Private working plan",
            AdventureLifecycleStage.Plan,
            PlanningStatus.Draft,
            PlanDates,
            Audit,
            travelers,
            destinationVisits,
            itineraryDays,
            activities,
            transportation,
            accommodations,
            reservations,
            notes: notes,
            tasks: tasks,
            budgetItems: budgetItems,
            packingItems: packingItems);
    }
}
