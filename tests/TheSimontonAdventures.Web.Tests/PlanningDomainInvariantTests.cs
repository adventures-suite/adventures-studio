using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Exercises negative invariants at the Planning Engine boundary.</summary>
public sealed class PlanningDomainInvariantTests
{
    private static readonly CreatorId CreatorId = new("creator_tsa_01");
    private static readonly PlanningDateRange Dates = new(
        new DateOnly(2027, 10, 25),
        new DateOnly(2027, 11, 15));
    private static readonly PlanAudit Audit = new(
        1,
        new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 7, 20, 30, 0, TimeSpan.Zero));

    /// <summary>Ensures every strongly typed identity rejects malformed values.</summary>
    [Fact]
    public void PlanningIdentities_InvalidValues_ThrowArgumentException()
    {
        Action<string>[] constructors =
        [
            value => _ = new AdventurePlanId(value),
            value => _ = new TravelerId(value),
            value => _ = new DestinationVisitId(value),
            value => _ = new ItineraryDayId(value),
            value => _ = new PlannedActivityId(value),
            value => _ = new TransportationSegmentId(value),
            value => _ = new AccommodationId(value),
            value => _ = new ReservationId(value),
            value => _ = new PlanningNoteId(value),
            value => _ = new PlanningTaskId(value),
            value => _ = new BudgetItemId(value),
            value => _ = new PackingItemId(value)
        ];

        foreach (var constructor in constructors)
        {
            Assert.Throws<ArgumentException>(() => constructor(string.Empty));
            Assert.Throws<ArgumentException>(() => constructor("Invalid-Id"));
        }
    }

    /// <summary>Ensures version and UTC audit ordering are validated.</summary>
    [Fact]
    public void PlanAudit_InvalidVersionOrOrdering_Throws()
    {
        var created = new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanAudit(0, created, created));
        Assert.Throws<ArgumentException>(() => new PlanAudit(1, created, created.AddMinutes(-1)));
    }

    /// <summary>Ensures invalid and default time zones cannot enter authoritative state.</summary>
    [Fact]
    public void TimeZone_InvalidOrDefault_Throws()
    {
        Assert.Throws<ArgumentException>(() => new IanaTimeZone("Eastern Standard Time"));
        Assert.Throws<ArgumentException>(() => new IanaTimeZone("Not/A_Time_Zone"));

        var visit = ValidVisit() with { TimeZone = default };
        Assert.Throws<ArgumentException>(() => CreatePlan(destinationVisits: [visit]));
    }

    /// <summary>Ensures caller-owned nested preference lists cannot mutate a plan snapshot.</summary>
    [Fact]
    public void Constructor_TravelerPreferences_AreDeeplyCopied()
    {
        var preferences = new List<string> { "Photography" };
        var traveler = new Traveler
        {
            Id = new TravelerId("traveler_steve"),
            DisplayName = "Steve",
            Preferences = preferences
        };
        var plan = CreatePlan(travelers: [traveler]);

        preferences.Add("Changed after construction");

        Assert.Single(plan.Travelers[0].Preferences);
        Assert.Equal("Photography", plan.Travelers[0].Preferences[0]);
    }

    /// <summary>Ensures null preferences fail with a predictable domain exception.</summary>
    [Fact]
    public void Constructor_NullTravelerPreferences_ThrowsArgumentException()
    {
        var traveler = new Traveler
        {
            Id = new TravelerId("traveler_steve"),
            DisplayName = "Steve",
            Preferences = null!
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(travelers: [traveler]));
    }

    /// <summary>Ensures caller-owned outer collections cannot mutate a plan snapshot.</summary>
    [Fact]
    public void Constructor_OuterCollections_AreDefensivelyCopied()
    {
        var notes = new List<PlanningNote>
        {
            new() { Id = new PlanningNoteId("note_private"), Text = "Original" }
        };
        var plan = CreatePlan(notes: notes);

        notes.Clear();

        Assert.Single(plan.Notes);
        Assert.Equal("Original", plan.Notes[0].Text);
    }

    /// <summary>Ensures lifecycle stage and planning maturity cannot contradict.</summary>
    [Theory]
    [InlineData(AdventureLifecycleStage.Remember, PlanningStatus.Idea)]
    [InlineData(AdventureLifecycleStage.Dream, PlanningStatus.Completed)]
    [InlineData(AdventureLifecycleStage.Publish, PlanningStatus.Draft)]
    public void Constructor_InvalidLifecycleStatusCombination_Throws(
        AdventureLifecycleStage lifecycle,
        PlanningStatus status)
    {
        Assert.Throws<ArgumentException>(() => CreatePlan(lifecycle: lifecycle, status: status));
    }

    /// <summary>Ensures undefined status values are rejected at construction.</summary>
    [Fact]
    public void Constructor_UnknownEnums_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(
            lifecycle: (AdventureLifecycleStage)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(
            status: (PlanningStatus)999));
    }

    /// <summary>Ensures referenced itinerary days stay inside their destination visit.</summary>
    [Fact]
    public void Constructor_ItineraryDayOutsideVisitDates_Throws()
    {
        var visit = ValidVisit();
        var day = ValidDay(visit) with { Date = visit.Dates.End.AddDays(1) };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [visit],
            itineraryDays: [day]));
    }

    /// <summary>Ensures referenced itinerary days use the destination visit time zone.</summary>
    [Fact]
    public void Constructor_ItineraryDayWithDifferentVisitTimeZone_Throws()
    {
        var visit = ValidVisit();
        var day = ValidDay(visit) with { TimeZone = new IanaTimeZone("America/New_York") };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [visit],
            itineraryDays: [day]));
    }

    /// <summary>Ensures activity references cannot point outside the aggregate.</summary>
    [Fact]
    public void Constructor_ActivityWithUnknownDay_Throws()
    {
        var activity = new PlannedActivity
        {
            Id = new PlannedActivityId("activity_prado"),
            ItineraryDayId = new ItineraryDayId("day_not_in_plan"),
            Title = "Prado Museum"
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(activities: [activity]));
    }

    /// <summary>Ensures visit order is positive and unique.</summary>
    [Fact]
    public void Constructor_InvalidVisitSequence_Throws()
    {
        var first = ValidVisit();
        var second = first with
        {
            Id = new DestinationVisitId("visit_barcelona"),
            Name = "Barcelona"
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [first with { Sequence = 0 }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [first, second]));
    }

    /// <summary>Appending a visit preserves existing state and advances exactly one version.</summary>
    [Fact]
    public void WithDestinationVisit_ValidVisit_AppendsAndAdvancesVersion()
    {
        var plan = CreatePlan();
        var visit = ValidVisit();
        var updatedAt = Audit.UpdatedAtUtc.AddHours(1);

        var updated = plan.WithDestinationVisit(visit, updatedAt);

        Assert.Single(updated.DestinationVisits);
        Assert.Equal(visit, updated.DestinationVisits[0]);
        Assert.Equal(plan.Audit.Version + 1, updated.Audit.Version);
        Assert.Equal(updatedAt, updated.Audit.UpdatedAtUtc);
        Assert.Empty(plan.DestinationVisits);
    }

    /// <summary>Appending a day preserves state and advances exactly one plan version.</summary>
    [Fact]
    public void WithItineraryDay_ValidDay_AppendsAndAdvancesVersion()
    {
        var visit = ValidVisit();
        var plan = CreatePlan(destinationVisits: [visit]);
        var day = ValidDay(visit);

        var updated = plan.WithItineraryDay(day, Audit.UpdatedAtUtc.AddHours(1));

        Assert.Equal(day, Assert.Single(updated.ItineraryDays));
        Assert.Equal(2, updated.Audit.Version);
        Assert.Empty(plan.ItineraryDays);
    }

    /// <summary>Appending an activity preserves state and advances one plan version.</summary>
    [Fact]
    public void WithPlannedActivity_ValidActivity_AppendsAndAdvancesVersion()
    {
        var visit = ValidVisit();
        var day = ValidDay(visit);
        var plan = CreatePlan(destinationVisits: [visit], itineraryDays: [day]);
        var activity = new PlannedActivity
        {
            Id = new("activity_museum"),
            ItineraryDayId = day.Id,
            Title = "Museum",
            StartsAtLocal = new(10, 0),
            EndsAtLocal = new(12, 0)
        };

        var updated = plan.WithPlannedActivity(activity, Audit.UpdatedAtUtc.AddHours(1));

        Assert.Equal(activity, Assert.Single(updated.Activities));
        Assert.Equal(2, updated.Audit.Version);
        Assert.Empty(plan.Activities);
    }

    /// <summary>Editing an activity preserves its identity, day, and status while advancing one version.</summary>
    [Fact]
    public void WithEditedPlannedActivity_ValidDetails_ReplacesOnlyEditableState()
    {
        var visit = ValidVisit();
        var day = ValidDay(visit);
        var activity = new PlannedActivity
        {
            Id = new("activity_museum"),
            ItineraryDayId = day.Id,
            Title = "Museum",
            StartsAtLocal = new(10, 0),
            EndsAtLocal = new(12, 0),
            Status = PlanItemStatus.Confirmed
        };
        var plan = CreatePlan(
            destinationVisits: [visit], itineraryDays: [day], activities: [activity]);

        var updated = plan.WithEditedPlannedActivity(
            activity.Id, "Gallery", new(11, 0), new(13, 0), Audit.UpdatedAtUtc.AddHours(1));

        var edited = Assert.Single(updated.Activities);
        Assert.Equal(activity.Id, edited.Id);
        Assert.Equal(activity.ItineraryDayId, edited.ItineraryDayId);
        Assert.Equal(activity.Status, edited.Status);
        Assert.Equal("Gallery", edited.Title);
        Assert.Equal(2, updated.Audit.Version);
        Assert.Equal("Museum", Assert.Single(plan.Activities).Title);
    }

    /// <summary>Appending transportation preserves state and advances one plan version.</summary>
    [Fact]
    public void WithTransportationSegment_ValidSegment_AppendsAndAdvancesVersion()
    {
        var plan = CreatePlan();
        var segment = ValidTransportation();

        var updated = plan.WithTransportationSegment(segment, Audit.UpdatedAtUtc.AddHours(1));

        Assert.Equal(segment, Assert.Single(updated.Transportation));
        Assert.Equal(2, updated.Audit.Version);
        Assert.Empty(plan.Transportation);
    }

    /// <summary>Editing transportation preserves identity, status, and unrelated plan state.</summary>
    [Fact]
    public void WithEditedTransportationSegment_ValidDetails_ReplacesOnlyEditableState()
    {
        var segment = new TransportationSegment
        {
            Id = new("transport_flight"),
            Mode = "Flight",
            From = "Phoenix",
            To = "Madrid",
            DepartureDate = new(2027, 10, 26),
            DepartureTimeLocal = new(18, 0),
            DepartureTimeZone = new("America/Phoenix"),
            ArrivalDate = new(2027, 10, 27),
            ArrivalTimeLocal = new(13, 0),
            ArrivalTimeZone = new("Europe/Madrid"),
            Status = PlanItemStatus.Confirmed
        };
        var note = new PlanningNote { Id = new("note_keep"), Text = "Preserve this note" };
        var plan = CreatePlan(transportation: [segment], notes: [note]);

        var updated = plan.WithEditedTransportationSegment(
            segment.Id, "Rail", "Madrid", "Barcelona", new(2027, 10, 28),
            new(9, 0), new("Europe/Madrid"), new(2027, 10, 28), new(12, 0),
            new("Europe/Madrid"), Audit.UpdatedAtUtc.AddHours(1));

        var edited = Assert.Single(updated.Transportation);
        Assert.Equal(segment.Id, edited.Id);
        Assert.Equal(segment.Status, edited.Status);
        Assert.Equal("Rail", edited.Mode);
        Assert.Equal(2, updated.Audit.Version);
        Assert.Equal(note, Assert.Single(updated.Notes));
        Assert.Equal("Flight", Assert.Single(plan.Transportation).Mode);
    }

    /// <summary>Appending accommodation preserves state and advances one version.</summary>
    [Fact]
    public void WithAccommodation_ValidAccommodation_AppendsAndAdvancesVersion()
    {
        var plan = CreatePlan();
        var accommodation = new Accommodation
        {
            Id = new("accommodation_rome"),
            Name = "Rome hotel",
            Dates = new(Dates.Start, Dates.Start.AddDays(2)),
            TimeZone = new("Europe/Rome")
        };
        var updated = plan.WithAccommodation(accommodation, Audit.UpdatedAtUtc.AddHours(1));
        Assert.Equal(accommodation, Assert.Single(updated.Accommodations));
        Assert.Equal(2, updated.Audit.Version);
    }

    /// <summary>Appending a reservation preserves state and advances one version.</summary>
    [Fact]
    public void WithReservation_ValidReservation_AppendsAndAdvancesVersion()
    {
        var plan = CreatePlan();
        var reservation = new Reservation
        {
            Id = new("reservation_prado_01"),
            Subject = "Prado Museum",
            Status = PlanItemStatus.Proposed
        };
        var updated = plan.WithReservation(reservation, Audit.UpdatedAtUtc.AddHours(1));
        Assert.Equal(reservation, Assert.Single(updated.Reservations));
        Assert.Equal(2, updated.Audit.Version);
    }

    /// <summary>Two itinerary days cannot represent the same local plan date.</summary>
    [Fact]
    public void Constructor_DuplicateItineraryDates_Throws()
    {
        var visit = ValidVisit();
        var first = ValidDay(visit);
        var second = first with { Id = new("day_duplicate_date") };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [visit], itineraryDays: [first, second]));
    }

    /// <summary>Ensures transportation range and status are valid.</summary>
    [Fact]
    public void Constructor_InvalidTransportation_Throws()
    {
        var transport = ValidTransportation();

        Assert.Throws<ArgumentException>(() => CreatePlan(
            transportation: [transport with { ArrivalDate = transport.DepartureDate.AddDays(-1) }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            transportation: [transport with { DepartureDate = Dates.Start.AddDays(-1) }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            transportation: [transport with { Status = (PlanItemStatus)999 }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(
            transportation:
            [
                transport with
                {
                    ArrivalDate = transport.DepartureDate,
                    ArrivalTimeZone = transport.DepartureTimeZone,
                    DepartureTimeLocal = new TimeOnly(18, 0),
                    ArrivalTimeLocal = new TimeOnly(10, 0)
                }
            ]));
    }

    /// <summary>Ensures accommodation range and status are valid.</summary>
    [Fact]
    public void Constructor_InvalidAccommodation_Throws()
    {
        var accommodation = new Accommodation
        {
            Id = new AccommodationId("stay_madrid"),
            Name = "Madrid hotel",
            Dates = new PlanningDateRange(Dates.Start, Dates.Start.AddDays(2)),
            TimeZone = new IanaTimeZone("Europe/Madrid")
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(accommodations:
            [accommodation with { Dates = new PlanningDateRange(Dates.Start.AddDays(-1), Dates.Start) }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(accommodations:
            [accommodation with { Status = (PlanItemStatus)999 }]));
    }

    /// <summary>Ensures reservation state and optional references are normalized.</summary>
    [Fact]
    public void Constructor_InvalidReservation_Throws()
    {
        var reservation = new Reservation
        {
            Id = new ReservationId("reservation_cruise"),
            Subject = "Cruise"
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePlan(reservations:
            [reservation with { Status = (PlanItemStatus)999 }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(reservations:
            [reservation with { ConfirmationReference = "  ABC123 " }]));
    }

    /// <summary>Ensures budget values use non-negative amounts and normalized currency.</summary>
    [Fact]
    public void Constructor_InvalidBudget_Throws()
    {
        var budget = new BudgetItem
        {
            Id = new BudgetItemId("budget_cruise"),
            Description = "Cruise",
            Amount = 100m,
            CurrencyCode = "USD"
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(budgetItems:
            [budget with { Amount = -1m }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(budgetItems:
            [budget with { CurrencyCode = "usd" }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(budgetItems:
            [budget with { CurrencyCode = null! }]));
    }

    /// <summary>Ensures blank and unnormalized child text is rejected.</summary>
    [Fact]
    public void Constructor_InvalidChildText_Throws()
    {
        Assert.Throws<ArgumentException>(() => CreatePlan(travelers:
            [new Traveler { Id = new TravelerId("traveler_steve"), DisplayName = " " }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(notes:
            [new PlanningNote { Id = new PlanningNoteId("note_private"), Text = " padded " }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(travelers:
            [
                new Traveler
                {
                    Id = new TravelerId("traveler_steve"),
                    DisplayName = "Steve",
                    Preferences = ["Photography", "photography"]
                }
            ]));
    }

    /// <summary>Ensures duplicate identities are rejected beyond traveler records.</summary>
    [Fact]
    public void Constructor_DuplicateNonTravelerIdentity_Throws()
    {
        var note = new PlanningNote
        {
            Id = new PlanningNoteId("note_private"),
            Text = "First"
        };

        Assert.Throws<ArgumentException>(() => CreatePlan(
            notes: [note, note with { Text = "Second" }]));

        var visit = ValidVisit();
        Assert.Throws<ArgumentException>(() => CreatePlan(
            destinationVisits: [visit, visit with { Name = "Duplicate" }]));

        var transport = ValidTransportation();
        Assert.Throws<ArgumentException>(() => CreatePlan(
            transportation: [transport, transport with { To = "Barcelona" }]));
    }

    /// <summary>Ensures default identities are rejected for every child category.</summary>
    [Fact]
    public void Constructor_DefaultChildIdentities_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreatePlan(destinationVisits:
            [ValidVisit() with { Id = default }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(itineraryDays:
            [
                new ItineraryDay
                {
                    Id = default,
                    Date = Dates.Start,
                    TimeZone = new IanaTimeZone("Europe/Madrid"),
                    Title = "Day"
                }
            ]));
        var day = new ItineraryDay
        {
            Id = new ItineraryDayId("day_madrid_one"),
            Date = Dates.Start,
            TimeZone = new IanaTimeZone("Europe/Madrid"),
            Title = "Day"
        };
        Assert.Throws<ArgumentException>(() => CreatePlan(
            itineraryDays: [day],
            activities:
            [
                new PlannedActivity
                {
                    Id = default,
                    ItineraryDayId = day.Id,
                    Title = "Activity"
                }
            ]));
        Assert.Throws<ArgumentException>(() => CreatePlan(transportation:
            [ValidTransportation() with { Id = default }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(accommodations:
            [
                new Accommodation
                {
                    Id = default,
                    Name = "Hotel",
                    Dates = new PlanningDateRange(Dates.Start, Dates.Start.AddDays(1)),
                    TimeZone = new IanaTimeZone("Europe/Madrid")
                }
            ]));
        Assert.Throws<ArgumentException>(() => CreatePlan(reservations:
            [new Reservation { Id = default, Subject = "Cruise" }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(notes:
            [new PlanningNote { Id = default, Text = "Note" }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(tasks:
            [new PlanningTask { Id = default, Description = "Task" }]));
        Assert.Throws<ArgumentException>(() => CreatePlan(budgetItems:
            [
                new BudgetItem
                {
                    Id = default,
                    Description = "Budget",
                    Amount = 1m,
                    CurrencyCode = "USD"
                }
            ]));
        Assert.Throws<ArgumentException>(() => CreatePlan(packingItems:
            [new PackingItem { Id = default, Description = "Camera" }]));
    }

    private static DestinationVisit ValidVisit() => new()
    {
        Id = new DestinationVisitId("visit_madrid"),
        Name = "Madrid",
        Dates = new PlanningDateRange(new DateOnly(2027, 10, 26), new DateOnly(2027, 10, 29)),
        TimeZone = new IanaTimeZone("Europe/Madrid"),
        Sequence = 1
    };

    private static ItineraryDay ValidDay(DestinationVisit visit) => new()
    {
        Id = new ItineraryDayId("day_madrid_one"),
        Date = visit.Dates.Start,
        TimeZone = visit.TimeZone,
        DestinationVisitId = visit.Id,
        Title = "Madrid"
    };

    private static TransportationSegment ValidTransportation() => new()
    {
        Id = new TransportationSegmentId("transport_madrid"),
        Mode = "Flight",
        From = "Phoenix",
        To = "Madrid",
        DepartureDate = Dates.Start,
        DepartureTimeZone = new IanaTimeZone("America/Phoenix"),
        ArrivalDate = Dates.Start.AddDays(1),
        ArrivalTimeZone = new IanaTimeZone("Europe/Madrid")
    };

    private static AdventurePlan CreatePlan(
        AdventureLifecycleStage lifecycle = AdventureLifecycleStage.Plan,
        PlanningStatus status = PlanningStatus.Draft,
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
            null,
            lifecycle,
            status,
            Dates,
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
