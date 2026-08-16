using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning;

/// <summary>
/// Owns one Creator's private, authoritative planning state independently of
/// public Content Engine records.
/// </summary>
public sealed class AdventurePlan
{
    /// <summary>Initializes and validates a complete private Adventure Plan snapshot.</summary>
    public AdventurePlan(
        AdventurePlanId id,
        CreatorId creatorId,
        string title,
        string? workingDescription,
        AdventureLifecycleStage lifecycleStage,
        PlanningStatus status,
        PlanningDateRange dates,
        PlanAudit audit,
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
        RequireIdentity(id, nameof(id));
        if (creatorId == default)
        {
            throw new ArgumentException("A valid Creator identity is required.", nameof(creatorId));
        }

        if (dates == default)
        {
            throw new ArgumentException("A planning date range is required.", nameof(dates));
        }

        if (audit == default)
        {
            throw new ArgumentException("Planning audit metadata is required.", nameof(audit));
        }

        if (!Enum.IsDefined(lifecycleStage))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleStage));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Id = id;
        CreatorId = creatorId;
        Title = RequireText(title, nameof(title));
        RequireOptionalText(workingDescription, nameof(workingDescription));
        WorkingDescription = workingDescription;
        LifecycleStage = lifecycleStage;
        Status = status;
        Dates = dates;
        Audit = audit;
        Travelers = CopyTravelers(travelers);
        DestinationVisits = Copy(destinationVisits);
        ItineraryDays = Copy(itineraryDays);
        Activities = Copy(activities);
        Transportation = Copy(transportation);
        Accommodations = Copy(accommodations);
        Reservations = Copy(reservations);
        Notes = Copy(notes);
        Tasks = Copy(tasks);
        BudgetItems = Copy(budgetItems);
        PackingItems = Copy(packingItems);

        ValidateLifecycleStatus(lifecycleStage, status);
        Validate();
    }

    /// <summary>Gets the stable plan identity.</summary>
    public AdventurePlanId Id { get; }
    /// <summary>Gets the immutable Creator ownership boundary.</summary>
    public CreatorId CreatorId { get; }
    /// <summary>Gets the private working title.</summary>
    public string Title { get; }
    /// <summary>Gets the optional private working description.</summary>
    public string? WorkingDescription { get; }
    /// <summary>Gets the broad lifecycle stage independently of planning maturity.</summary>
    public AdventureLifecycleStage LifecycleStage { get; }
    /// <summary>Gets the private planning status, which never implies publication.</summary>
    public PlanningStatus Status { get; }
    /// <summary>Gets the inclusive local-calendar planning range.</summary>
    public PlanningDateRange Dates { get; }
    /// <summary>Gets UTC audit and optimistic-concurrency metadata.</summary>
    public PlanAudit Audit { get; }
    /// <summary>Gets the minimum-safe traveler records.</summary>
    public IReadOnlyList<Traveler> Travelers { get; }
    /// <summary>Gets ordered destination visits.</summary>
    public IReadOnlyList<DestinationVisit> DestinationVisits { get; }
    /// <summary>Gets local itinerary days.</summary>
    public IReadOnlyList<ItineraryDay> ItineraryDays { get; }
    /// <summary>Gets planned activities.</summary>
    public IReadOnlyList<PlannedActivity> Activities { get; }
    /// <summary>Gets transportation segments.</summary>
    public IReadOnlyList<TransportationSegment> Transportation { get; }
    /// <summary>Gets planned accommodations.</summary>
    public IReadOnlyList<Accommodation> Accommodations { get; }
    /// <summary>Gets private reservation summaries.</summary>
    public IReadOnlyList<Reservation> Reservations { get; }
    /// <summary>Gets private planning notes.</summary>
    public IReadOnlyList<PlanningNote> Notes { get; }
    /// <summary>Gets planning tasks.</summary>
    public IReadOnlyList<PlanningTask> Tasks { get; }
    /// <summary>Gets private budget items.</summary>
    public IReadOnlyList<BudgetItem> BudgetItems { get; }
    /// <summary>Gets private packing items.</summary>
    public IReadOnlyList<PackingItem> PackingItems { get; }

    /// <summary>
    /// Creates the next validated aggregate version with only overview fields changed.
    /// All lifecycle state and child records are preserved.
    /// </summary>
    public AdventurePlan WithOverview(
        string title,
        string? workingDescription,
        PlanningDateRange dates,
        DateTimeOffset updatedAtUtc) => new(
        Id,
        CreatorId,
        title,
        workingDescription,
        LifecycleStage,
        Status,
        dates,
        new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
        Travelers,
        DestinationVisits,
        ItineraryDays,
        Activities,
        Transportation,
        Accommodations,
        Reservations,
        Notes,
        Tasks,
        BudgetItems,
        PackingItems);

    /// <summary>
    /// Creates the next validated aggregate version with one destination visit appended.
    /// Existing plan fields and child records are preserved.
    /// </summary>
    public AdventurePlan WithDestinationVisit(
        DestinationVisit destinationVisit,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(destinationVisit);
        return new(
            Id,
            CreatorId,
            Title,
            WorkingDescription,
            LifecycleStage,
            Status,
            Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers,
            [.. DestinationVisits, destinationVisit],
            ItineraryDays,
            Activities,
            Transportation,
            Accommodations,
            Reservations,
            Notes,
            Tasks,
            BudgetItems,
            PackingItems);
    }

    /// <summary>
    /// Creates the next validated aggregate version with one local itinerary day appended.
    /// Existing plan fields and child records are preserved.
    /// </summary>
    public AdventurePlan WithItineraryDay(
        ItineraryDay itineraryDay,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(itineraryDay);
        return new(
            Id,
            CreatorId,
            Title,
            WorkingDescription,
            LifecycleStage,
            Status,
            Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers,
            DestinationVisits,
            [.. ItineraryDays, itineraryDay],
            Activities,
            Transportation,
            Accommodations,
            Reservations,
            Notes,
            Tasks,
            BudgetItems,
            PackingItems);
    }

    /// <summary>
    /// Creates the next validated aggregate version with one proposed activity appended.
    /// Existing plan fields and child records are preserved.
    /// </summary>
    public AdventurePlan WithPlannedActivity(
        PlannedActivity activity,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return new(
            Id,
            CreatorId,
            Title,
            WorkingDescription,
            LifecycleStage,
            Status,
            Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers,
            DestinationVisits,
            ItineraryDays,
            [.. Activities, activity],
            Transportation,
            Accommodations,
            Reservations,
            Notes,
            Tasks,
            BudgetItems,
            PackingItems);
    }

    /// <summary>
    /// Creates the next aggregate version with one existing activity's editable details replaced.
    /// Its identity, itinerary-day relationship, and planning status are preserved.
    /// </summary>
    public AdventurePlan WithEditedPlannedActivity(
        PlannedActivityId activityId,
        string title,
        TimeOnly? startsAtLocal,
        TimeOnly? endsAtLocal,
        DateTimeOffset updatedAtUtc)
    {
        var existing = Activities.SingleOrDefault(item => item.Id == activityId)
            ?? throw new ArgumentException("The planned activity must belong to this plan.", nameof(activityId));
        var replacement = new PlannedActivity
        {
            Id = existing.Id,
            ItineraryDayId = existing.ItineraryDayId,
            Title = title,
            StartsAtLocal = startsAtLocal,
            EndsAtLocal = endsAtLocal,
            Status = existing.Status
        };
        var activities = Activities.Select(item => item.Id == activityId ? replacement : item).ToArray();
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, activities, Transportation,
            Accommodations, Reservations, Notes, Tasks, BudgetItems, PackingItems);
    }

    /// <summary>
    /// Creates the next validated aggregate version with one transportation segment appended.
    /// Existing plan fields and child records are preserved.
    /// </summary>
    public AdventurePlan WithTransportationSegment(
        TransportationSegment segment,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, Activities,
            [.. Transportation, segment], Accommodations, Reservations, Notes, Tasks,
            BudgetItems, PackingItems);
    }

    /// <summary>
    /// Creates the next aggregate version with one existing transportation segment's editable
    /// details replaced while preserving its identity and planning status.
    /// </summary>
    public AdventurePlan WithEditedTransportationSegment(
        TransportationSegmentId segmentId,
        string mode,
        string from,
        string to,
        DateOnly departureDate,
        TimeOnly? departureTimeLocal,
        IanaTimeZone departureTimeZone,
        DateOnly arrivalDate,
        TimeOnly? arrivalTimeLocal,
        IanaTimeZone arrivalTimeZone,
        DateTimeOffset updatedAtUtc)
    {
        var existing = Transportation.SingleOrDefault(item => item.Id == segmentId)
            ?? throw new ArgumentException(
                "The transportation segment must belong to this plan.", nameof(segmentId));
        var replacement = new TransportationSegment
        {
            Id = existing.Id,
            Mode = mode,
            From = from,
            To = to,
            DepartureDate = departureDate,
            DepartureTimeLocal = departureTimeLocal,
            DepartureTimeZone = departureTimeZone,
            ArrivalDate = arrivalDate,
            ArrivalTimeLocal = arrivalTimeLocal,
            ArrivalTimeZone = arrivalTimeZone,
            Status = existing.Status
        };
        var transportation = Transportation.Select(
            item => item.Id == segmentId ? replacement : item).ToArray();
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, Activities, transportation,
            Accommodations, Reservations, Notes, Tasks, BudgetItems, PackingItems);
    }

    /// <summary>Creates the next validated version with one proposed accommodation appended.</summary>
    public AdventurePlan WithAccommodation(
        Accommodation accommodation,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(accommodation);
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, Activities, Transportation,
            [.. Accommodations, accommodation], Reservations, Notes, Tasks,
            BudgetItems, PackingItems);
    }

    /// <summary>
    /// Creates the next aggregate version with one existing accommodation's editable details
    /// replaced while preserving its identity, planning status, and list position.
    /// </summary>
    public AdventurePlan WithEditedAccommodation(
        AccommodationId accommodationId,
        string name,
        PlanningDateRange dates,
        IanaTimeZone timeZone,
        DateTimeOffset updatedAtUtc)
    {
        var existing = Accommodations.SingleOrDefault(item => item.Id == accommodationId)
            ?? throw new ArgumentException(
                "The accommodation must belong to this plan.", nameof(accommodationId));
        var replacement = new Accommodation
        {
            Id = existing.Id,
            Name = name,
            Dates = dates,
            TimeZone = timeZone,
            Status = existing.Status
        };
        var accommodations = Accommodations.Select(
            item => item.Id == accommodationId ? replacement : item).ToArray();
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, Activities, Transportation,
            accommodations, Reservations, Notes, Tasks, BudgetItems, PackingItems);
    }

    /// <summary>Creates the next validated version with one proposed reservation appended.</summary>
    public AdventurePlan WithReservation(
        Reservation reservation,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        return new(
            Id, CreatorId, Title, WorkingDescription, LifecycleStage, Status, Dates,
            new PlanAudit(checked(Audit.Version + 1), Audit.CreatedAtUtc, updatedAtUtc),
            Travelers, DestinationVisits, ItineraryDays, Activities, Transportation,
            Accommodations, [.. Reservations, reservation], Notes, Tasks,
            BudgetItems, PackingItems);
    }

    private void Validate()
    {
        ValidateUnique(Travelers, item => item.Id, "traveler");
        ValidateUnique(DestinationVisits, item => item.Id, "destination visit");
        ValidateUnique(ItineraryDays, item => item.Id, "itinerary day");
        ValidateUnique(Activities, item => item.Id, "activity");
        ValidateUnique(Transportation, item => item.Id, "transportation segment");
        ValidateUnique(Accommodations, item => item.Id, "accommodation");
        ValidateUnique(Reservations, item => item.Id, "reservation");
        ValidateUnique(Notes, item => item.Id, "note");
        ValidateUnique(Tasks, item => item.Id, "task");
        ValidateUnique(BudgetItems, item => item.Id, "budget item");
        ValidateUnique(PackingItems, item => item.Id, "packing item");

        foreach (var traveler in Travelers)
        {
            RequireIdentity(traveler.Id, nameof(traveler.Id));
            RequireText(traveler.DisplayName, nameof(traveler.DisplayName));
            ValidateTextCollection(traveler.Preferences, "Traveler preferences");
        }

        foreach (var visit in DestinationVisits)
        {
            RequireIdentity(visit.Id, nameof(visit.Id));
            RequireTimeZone(visit.TimeZone, nameof(visit.TimeZone));
            RequireText(visit.Name, nameof(visit.Name));
            RequireOptionalText(visit.Notes, nameof(visit.Notes));
            if (!Dates.Contains(visit.Dates) || visit.Sequence < 1)
            {
                throw new ArgumentException("Destination visits must be ordered and fall within plan dates.");
            }
        }

        if (DestinationVisits.Select(item => item.Sequence).Distinct().Count() != DestinationVisits.Count)
        {
            throw new ArgumentException("Destination visit sequence values must be unique.");
        }

        var dayIds = ItineraryDays.Select(item => item.Id).ToHashSet();
        if (ItineraryDays.Select(item => item.Date).Distinct().Count() != ItineraryDays.Count)
        {
            throw new ArgumentException("Itinerary day dates must be unique within a plan.");
        }

        var visitsById = DestinationVisits.ToDictionary(item => item.Id);
        foreach (var day in ItineraryDays)
        {
            RequireIdentity(day.Id, nameof(day.Id));
            RequireTimeZone(day.TimeZone, nameof(day.TimeZone));
            RequireText(day.Title, nameof(day.Title));
            if (!Dates.Contains(day.Date))
            {
                throw new ArgumentException("Itinerary days must fall within plan dates and reference this plan's visits.");
            }

            if (day.DestinationVisitId is { } visitId)
            {
                if (!visitsById.TryGetValue(visitId, out var visit)
                    || !visit.Dates.Contains(day.Date)
                    || visit.TimeZone != day.TimeZone)
                {
                    throw new ArgumentException(
                        "A destination itinerary day must use the referenced visit's dates and time zone.");
                }
            }
        }

        foreach (var activity in Activities)
        {
            RequireIdentity(activity.Id, nameof(activity.Id));
            RequireText(activity.Title, nameof(activity.Title));
            if (!dayIds.Contains(activity.ItineraryDayId)
                || !Enum.IsDefined(activity.Status)
                || (activity.StartsAtLocal is { } start && activity.EndsAtLocal is { } end && end < start))
            {
                throw new ArgumentException("Activities must reference this plan's day and have a valid local-time range.");
            }
        }

        foreach (var segment in Transportation)
        {
            RequireIdentity(segment.Id, nameof(segment.Id));
            RequireTimeZone(segment.DepartureTimeZone, nameof(segment.DepartureTimeZone));
            RequireTimeZone(segment.ArrivalTimeZone, nameof(segment.ArrivalTimeZone));
            RequireText(segment.Mode, nameof(segment.Mode));
            RequireText(segment.From, nameof(segment.From));
            RequireText(segment.To, nameof(segment.To));
            if (!Enum.IsDefined(segment.Status)
                || segment.ArrivalDate < segment.DepartureDate
                || !Dates.Contains(segment.DepartureDate)
                || !Dates.Contains(segment.ArrivalDate)
                || (segment.DepartureDate == segment.ArrivalDate
                    && segment.DepartureTimeZone == segment.ArrivalTimeZone
                    && segment.DepartureTimeLocal is { } departureTime
                    && segment.ArrivalTimeLocal is { } arrivalTime
                    && arrivalTime < departureTime))
            {
                throw new ArgumentException("Transportation arrival date cannot precede departure date.");
            }
        }

        foreach (var accommodation in Accommodations)
        {
            RequireIdentity(accommodation.Id, nameof(accommodation.Id));
            RequireTimeZone(accommodation.TimeZone, nameof(accommodation.TimeZone));
            RequireText(accommodation.Name, nameof(accommodation.Name));
            if (!Enum.IsDefined(accommodation.Status) || !Dates.Contains(accommodation.Dates))
            {
                throw new ArgumentException("Accommodation dates must fall within plan dates.");
            }
        }

        foreach (var item in Reservations)
        {
            RequireIdentity(item.Id, nameof(item.Id));
            RequireText(item.Subject, nameof(item.Subject));
            RequireOptionalText(item.ConfirmationReference, nameof(item.ConfirmationReference));
            if (!Enum.IsDefined(item.Status))
            {
                throw new ArgumentOutOfRangeException(nameof(item.Status));
            }
        }

        foreach (var item in Notes)
        {
            RequireIdentity(item.Id, nameof(item.Id));
            RequireText(item.Text, nameof(item.Text));
        }

        foreach (var item in Tasks)
        {
            RequireIdentity(item.Id, nameof(item.Id));
            RequireText(item.Description, nameof(item.Description));
        }

        foreach (var item in BudgetItems)
        {
            RequireIdentity(item.Id, nameof(item.Id));
            RequireText(item.Description, nameof(item.Description));
            var currencyCode = RequireText(item.CurrencyCode, nameof(item.CurrencyCode));
            if (item.Amount < 0 || currencyCode.Length != 3 || currencyCode.Any(character => character is < 'A' or > 'Z'))
            {
                throw new ArgumentException("Budget amounts must be non-negative and use an uppercase ISO currency code.");
            }
        }

        foreach (var item in PackingItems)
        {
            RequireIdentity(item.Id, nameof(item.Id));
            RequireText(item.Description, nameof(item.Description));
        }
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T>? items) =>
        items is null ? [] : Array.AsReadOnly(items.ToArray());

    private static IReadOnlyList<Traveler> CopyTravelers(IReadOnlyList<Traveler>? travelers)
    {
        if (travelers is null)
        {
            return [];
        }

        var copies = new Traveler[travelers.Count];
        for (var index = 0; index < travelers.Count; index++)
        {
            var traveler = travelers[index]
                ?? throw new ArgumentException("Traveler records cannot be null.", nameof(travelers));
            var preferences = traveler.Preferences
                ?? throw new ArgumentException("Traveler preferences cannot be null.", nameof(travelers));

            copies[index] = traveler with
            {
                Preferences = Array.AsReadOnly(preferences.ToArray())
            };
        }

        return Array.AsReadOnly(copies);
    }

    private static string RequireText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value;
    }

    private static void RequireOptionalText(string? value, string parameterName)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value != value.Trim()))
        {
            throw new ArgumentException(
                "Optional text must be non-empty and cannot contain surrounding whitespace.",
                parameterName);
        }
    }

    private static void ValidateTextCollection(IReadOnlyList<string> values, string label)
    {
        if (values.Any(value => string.IsNullOrWhiteSpace(value) || value != value.Trim())
            || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
        {
            throw new ArgumentException($"{label} must be non-empty, normalized, and unique.");
        }
    }

    private static void ValidateLifecycleStatus(
        AdventureLifecycleStage lifecycleStage,
        PlanningStatus status)
    {
        var isValid = lifecycleStage switch
        {
            AdventureLifecycleStage.Dream => status is PlanningStatus.Idea or PlanningStatus.Draft,
            AdventureLifecycleStage.Plan => status is PlanningStatus.Draft or PlanningStatus.Planned or PlanningStatus.Upcoming,
            AdventureLifecycleStage.Travel => status is PlanningStatus.Upcoming or PlanningStatus.InProgress or PlanningStatus.Completed,
            AdventureLifecycleStage.Preserve or AdventureLifecycleStage.Publish or AdventureLifecycleStage.Share =>
                status is PlanningStatus.Completed,
            AdventureLifecycleStage.Remember => status is PlanningStatus.Completed or PlanningStatus.Archived,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentException(
                $"Planning status '{status}' is not valid during lifecycle stage '{lifecycleStage}'.");
        }
    }

    private static void RequireIdentity<T>(T value, string parameterName) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException("A non-default identity is required.", parameterName);
        }
    }

    private static void RequireTimeZone(IanaTimeZone value, string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException("A non-default IANA time zone is required.", parameterName);
        }
    }

    private static void ValidateUnique<TItem, TId>(
        IReadOnlyList<TItem> items,
        Func<TItem, TId> identity,
        string label)
        where TId : notnull
    {
        if (items.Select(identity).Distinct().Count() != items.Count)
        {
            throw new ArgumentException($"Duplicate {label} identities are not allowed.");
        }
    }
}
