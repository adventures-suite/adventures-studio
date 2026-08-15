using TheSimontonAdventures.Web.Creators;

namespace TheSimontonAdventures.Web.Planning.Persistence;

/// <summary>
/// Defines provider-independent persistence operations for private,
/// Creator-scoped Adventure Plans.
/// </summary>
public interface IAdventurePlanRepository
{
    /// <summary>Gets minimum ownership and lifecycle facts for authorization.</summary>
    Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists minimum non-sensitive dashboard projections for active plans.</summary>
    Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets an allowlisted, non-sensitive read-only plan projection.</summary>
    Task<AdventurePlanDetail?> GetDetailAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves one plan only within the supplied Creator boundary.</summary>
    Task<AdventurePlan?> GetAsync(
        CreatorId creatorId,
        AdventurePlanId planId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists non-archived plans owned by the supplied Creator.</summary>
    Task<IReadOnlyList<AdventurePlan>> ListAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists recoverable archived plans owned by the supplied Creator.</summary>
    Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(
        CreatorId creatorId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new plan whose ownership matches the supplied Creator.</summary>
    Task AddAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a plan when its persisted version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task UpdateAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only overview columns when the persisted version matches, without
    /// replacing or rewriting any child records.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task UpdateOverviewAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one destination visit and advances the owning plan when the
    /// persisted version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task AddDestinationVisitAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        DestinationVisit destinationVisit,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one itinerary day and advances the owning plan when the persisted
    /// version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task AddItineraryDayAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        ItineraryDay itineraryDay,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one planned activity and advances the owning plan when the
    /// persisted version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task AddPlannedActivityAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        PlannedActivity activity,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one transportation segment and advances the owning plan when the
    /// persisted version matches the expected version.
    /// </summary>
    /// <exception cref="PlanningConcurrencyException">
    /// The persisted plan has changed since <paramref name="expectedVersion"/>.
    /// </exception>
    Task AddTransportationSegmentAsync(
        CreatorId creatorId,
        AdventurePlan plan,
        TransportationSegment segment,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains only the authoritative facts needed to authorize one plan.</summary>
public sealed record AdventurePlanAuthorizationFacts
{
    /// <summary>Gets the owning Creator identity.</summary>
    public required CreatorId CreatorId { get; init; }
    /// <summary>Gets the stable plan identity.</summary>
    public required AdventurePlanId PlanId { get; init; }
    /// <summary>Gets whether the plan is recoverably archived.</summary>
    public required bool IsArchived { get; init; }
    /// <summary>Gets the authoritative optimistic-concurrency version.</summary>
    public required long Version { get; init; }
}

/// <summary>Projects only fields approved for the read-only Planner dashboard.</summary>
public sealed record AdventurePlanDashboardItem
{
    /// <summary>Gets the stable plan identity.</summary>
    public required AdventurePlanId Id { get; init; }
    /// <summary>Gets the private working title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the lifecycle stage independently of publication.</summary>
    public required AdventureLifecycleStage LifecycleStage { get; init; }
    /// <summary>Gets the private planning status.</summary>
    public required PlanningStatus Status { get; init; }
    /// <summary>Gets the local-calendar planning range.</summary>
    public required PlanningDateRange Dates { get; init; }
    /// <summary>Gets the authoritative optimistic-concurrency version.</summary>
    public required long Version { get; init; }
    /// <summary>Gets whether the plan is recoverably archived.</summary>
    public required bool IsArchived { get; init; }
}

/// <summary>Projects an allowlisted private plan view without sensitive operational fields.</summary>
public sealed record AdventurePlanDetail
{
    /// <summary>Gets the stable plan identity.</summary>
    public required AdventurePlanId Id { get; init; }
    /// <summary>Gets the private working title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets the optional working description.</summary>
    public string? WorkingDescription { get; init; }
    /// <summary>Gets the lifecycle stage independently of publication.</summary>
    public required AdventureLifecycleStage LifecycleStage { get; init; }
    /// <summary>Gets the private planning status.</summary>
    public required PlanningStatus Status { get; init; }
    /// <summary>Gets the local-calendar planning range.</summary>
    public required PlanningDateRange Dates { get; init; }
    /// <summary>Gets the authoritative optimistic-concurrency version.</summary>
    public required long Version { get; init; }
    /// <summary>Gets the number of travelers without exposing their identities.</summary>
    public required int TravelerCount { get; init; }
    /// <summary>Gets ordered destination visits without private visit notes.</summary>
    public IReadOnlyList<DestinationVisitDetail> Destinations { get; init; } = [];
    /// <summary>Gets itinerary days and activities in local calendar context.</summary>
    public IReadOnlyList<ItineraryDayDetail> Days { get; init; } = [];
    /// <summary>Gets provider-neutral transportation summaries.</summary>
    public IReadOnlyList<TransportationDetail> Transportation { get; init; } = [];
    /// <summary>Gets accommodation summaries without reservation credentials.</summary>
    public IReadOnlyList<AccommodationDetail> Accommodations { get; init; } = [];
}

/// <summary>Projects one ordered destination visit without private notes.</summary>
public sealed record DestinationVisitDetail(
    DestinationVisitId Id,
    string Name,
    PlanningDateRange Dates,
    IanaTimeZone TimeZone,
    int Sequence);

/// <summary>Projects one local itinerary day and its allowlisted activities.</summary>
public sealed record ItineraryDayDetail(
    ItineraryDayId Id,
    DestinationVisitId? DestinationVisitId,
    DateOnly Date,
    IanaTimeZone TimeZone,
    string Title,
    IReadOnlyList<ActivityDetail> Activities);

/// <summary>Projects one planned activity without reservation linkage or credentials.</summary>
public sealed record ActivityDetail(
    PlannedActivityId Id,
    string Title,
    TimeOnly? StartsAtLocal,
    TimeOnly? EndsAtLocal,
    PlanItemStatus Status);

/// <summary>Projects one provider-neutral transportation segment.</summary>
public sealed record TransportationDetail(
    TransportationSegmentId Id,
    string Mode,
    string From,
    string To,
    DateOnly DepartureDate,
    TimeOnly? DepartureTimeLocal,
    IanaTimeZone DepartureTimeZone,
    DateOnly ArrivalDate,
    TimeOnly? ArrivalTimeLocal,
    IanaTimeZone ArrivalTimeZone,
    PlanItemStatus Status);

/// <summary>Projects one accommodation without confirmation or booking credentials.</summary>
public sealed record AccommodationDetail(
    AccommodationId Id,
    string Name,
    PlanningDateRange Dates,
    IanaTimeZone TimeZone,
    PlanItemStatus Status);
