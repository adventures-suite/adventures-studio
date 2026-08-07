namespace TheSimontonAdventures.Web.Planning;

/// <summary>Represents a participant using only minimum-safe planning details.</summary>
public sealed record Traveler
{
    /// <summary>Gets the stable traveler identity.</summary>
    public required TravelerId Id { get; init; }
    /// <summary>Gets the travel-facing display name.</summary>
    public required string DisplayName { get; init; }
    /// <summary>Gets non-sensitive travel preferences.</summary>
    public IReadOnlyList<string> Preferences { get; init; } = [];
}

/// <summary>Represents an ordered, local-date visit to a destination.</summary>
public sealed record DestinationVisit
{
    /// <summary>Gets the stable visit identity.</summary>
    public required DestinationVisitId Id { get; init; }
    /// <summary>Gets the working destination name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the inclusive expected local-date range.</summary>
    public required PlanningDateRange Dates { get; init; }
    /// <summary>Gets the destination's IANA time-zone identity.</summary>
    public required IanaTimeZone TimeZone { get; init; }
    /// <summary>Gets the route order within the plan.</summary>
    public required int Sequence { get; init; }
    /// <summary>Gets private visit-specific notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>Represents one itinerary day in its applicable local calendar.</summary>
public sealed record ItineraryDay
{
    /// <summary>Gets the stable itinerary-day identity.</summary>
    public required ItineraryDayId Id { get; init; }
    /// <summary>Gets the local calendar date without UTC conversion.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the local IANA time zone for the day.</summary>
    public required IanaTimeZone TimeZone { get; init; }
    /// <summary>Gets the associated visit, or no value for transit and sea days.</summary>
    public DestinationVisitId? DestinationVisitId { get; init; }
    /// <summary>Gets the day's working title.</summary>
    public required string Title { get; init; }
}

/// <summary>Represents an activity proposed or managed within a local itinerary day.</summary>
public sealed record PlannedActivity
{
    /// <summary>Gets the stable activity identity.</summary>
    public required PlannedActivityId Id { get; init; }
    /// <summary>Gets the owning itinerary day.</summary>
    public required ItineraryDayId ItineraryDayId { get; init; }
    /// <summary>Gets the activity title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the optional local start time.</summary>
    public TimeOnly? StartsAtLocal { get; init; }
    /// <summary>Gets the optional local end time.</summary>
    public TimeOnly? EndsAtLocal { get; init; }
    /// <summary>Gets the operational planning state.</summary>
    public PlanItemStatus Status { get; init; } = PlanItemStatus.Proposed;
}

/// <summary>Represents provider-independent transportation between places.</summary>
public sealed record TransportationSegment
{
    /// <summary>Gets the stable transportation identity.</summary>
    public required TransportationSegmentId Id { get; init; }
    /// <summary>Gets the transportation mode such as flight, rail, cruise, or car.</summary>
    public required string Mode { get; init; }
    /// <summary>Gets the departure place.</summary>
    public required string From { get; init; }
    /// <summary>Gets the arrival place.</summary>
    public required string To { get; init; }
    /// <summary>Gets the local departure date.</summary>
    public required DateOnly DepartureDate { get; init; }
    /// <summary>Gets the optional local departure time.</summary>
    public TimeOnly? DepartureTimeLocal { get; init; }
    /// <summary>Gets the departure IANA time zone.</summary>
    public required IanaTimeZone DepartureTimeZone { get; init; }
    /// <summary>Gets the local arrival date.</summary>
    public required DateOnly ArrivalDate { get; init; }
    /// <summary>Gets the optional local arrival time.</summary>
    public TimeOnly? ArrivalTimeLocal { get; init; }
    /// <summary>Gets the arrival IANA time zone.</summary>
    public required IanaTimeZone ArrivalTimeZone { get; init; }
    /// <summary>Gets the operational planning state.</summary>
    public PlanItemStatus Status { get; init; } = PlanItemStatus.Proposed;
}

/// <summary>Represents a provider-independent planned stay.</summary>
public sealed record Accommodation
{
    /// <summary>Gets the stable accommodation identity.</summary>
    public required AccommodationId Id { get; init; }
    /// <summary>Gets the working accommodation name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the inclusive local stay dates.</summary>
    public required PlanningDateRange Dates { get; init; }
    /// <summary>Gets the property's IANA time zone.</summary>
    public required IanaTimeZone TimeZone { get; init; }
    /// <summary>Gets the operational planning state.</summary>
    public PlanItemStatus Status { get; init; } = PlanItemStatus.Proposed;
}

/// <summary>Represents a private reservation summary without payment data.</summary>
public sealed record Reservation
{
    /// <summary>Gets the stable reservation identity.</summary>
    public required ReservationId Id { get; init; }
    /// <summary>Gets the reservation subject.</summary>
    public required string Subject { get; init; }
    /// <summary>Gets an optional private confirmation reference.</summary>
    public string? ConfirmationReference { get; init; }
    /// <summary>Gets the operational planning state.</summary>
    public PlanItemStatus Status { get; init; } = PlanItemStatus.Proposed;
}

/// <summary>Represents a private note that is never public by implication.</summary>
public sealed record PlanningNote
{
    /// <summary>Gets the stable note identity.</summary>
    public required PlanningNoteId Id { get; init; }
    /// <summary>Gets the private note text.</summary>
    public required string Text { get; init; }
}

/// <summary>Represents one actionable planning task.</summary>
public sealed record PlanningTask
{
    /// <summary>Gets the stable task identity.</summary>
    public required PlanningTaskId Id { get; init; }
    /// <summary>Gets the task description.</summary>
    public required string Description { get; init; }
    /// <summary>Gets the optional local due date.</summary>
    public DateOnly? DueDate { get; init; }
    /// <summary>Gets whether the task is complete.</summary>
    public bool IsCompleted { get; init; }
}

/// <summary>Represents one private estimated or actual expense.</summary>
public sealed record BudgetItem
{
    /// <summary>Gets the stable budget-item identity.</summary>
    public required BudgetItemId Id { get; init; }
    /// <summary>Gets the expense description.</summary>
    public required string Description { get; init; }
    /// <summary>Gets the non-negative monetary amount.</summary>
    public required decimal Amount { get; init; }
    /// <summary>Gets the ISO 4217 currency code.</summary>
    public required string CurrencyCode { get; init; }
}

/// <summary>Represents one private packing-list entry.</summary>
public sealed record PackingItem
{
    /// <summary>Gets the stable packing-item identity.</summary>
    public required PackingItemId Id { get; init; }
    /// <summary>Gets the item description.</summary>
    public required string Description { get; init; }
    /// <summary>Gets whether the item has been packed.</summary>
    public bool IsPacked { get; init; }
}
