using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Defines the maximum authoritative Adventure page size.</summary>
public static class CompanionReadProjectionLimits
{
    /// <summary>Gets the maximum number of Adventure summaries returned by one query.</summary>
    public const int MaximumAdventures = 50;
}

/// <summary>Contains the server-established scope for an authoritative Companion read.</summary>
public sealed record CompanionAdventureReadScope(
    CreatorId CreatorId,
    UserId UserId,
    long MembershipVersion,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>Represents traveler-facing Adventure lifecycle state.</summary>
public enum CompanionAdventureLifecycle
{
    /// <summary>The Adventure is approved but not yet committed.</summary>
    Planned,
    /// <summary>The Adventure is committed and upcoming.</summary>
    Committed,
    /// <summary>The Adventure is currently in progress.</summary>
    InProgress,
    /// <summary>The Adventure is complete.</summary>
    Completed
}

/// <summary>Provides the minimum authorized Adventure summary projection.</summary>
public sealed record CompanionAdventureSummaryProjection(
    string AdventureId,
    string TravelerId,
    string Title,
    CompanionAdventureLifecycle Lifecycle,
    DateOnly StartDate,
    DateOnly EndDate,
    string PrimaryTimeZone,
    long PlanVersion,
    long ParticipationVersion,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Provides one ordered traveler-safe destination projection.</summary>
public sealed record CompanionDestinationProjection(
    string DestinationVisitId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZone,
    int Sequence);

/// <summary>Provides the authorized Adventure detail projection.</summary>
public sealed record CompanionAdventureDetailProjection(
    CompanionAdventureSummaryProjection Adventure,
    string? Description,
    IReadOnlyList<CompanionDestinationProjection> Destinations);

/// <summary>Contains the server-established scope for one Today projection.</summary>
public sealed record CompanionTodayReadScope(
    CreatorId CreatorId,
    UserId UserId,
    string TravelerId,
    long MembershipVersion,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>Describes the authorized local-day position without using wire-contract types.</summary>
public enum CompanionTodayProjectionState
{
    /// <summary>The Adventure has not begun.</summary>
    BeforeAdventure,
    /// <summary>The Adventure is active and has visible items today.</summary>
    Active,
    /// <summary>The Adventure has ended.</summary>
    AfterAdventure,
    /// <summary>The Adventure is active without visible scheduled items today.</summary>
    NoScheduledItems
}

/// <summary>Describes local schedule timing without using wire-contract types.</summary>
public enum CompanionScheduleTimeState
{
    /// <summary>The item has an explicit local time.</summary>
    Scheduled,
    /// <summary>The item is explicitly all-day.</summary>
    AllDay,
    /// <summary>The item's local time remains to be confirmed.</summary>
    ToBeConfirmed,
    /// <summary>The item is cancelled.</summary>
    Cancelled
}

/// <summary>Describes operational schedule state without using wire-contract types.</summary>
public enum CompanionScheduleOperationalState
{
    /// <summary>The item is proposed.</summary>
    Proposed,
    /// <summary>The item is reserved without implying platform booking.</summary>
    Reserved,
    /// <summary>The item is confirmed by the authoritative projection.</summary>
    Confirmed,
    /// <summary>The item materially changed.</summary>
    Changed,
    /// <summary>The item is cancelled.</summary>
    Cancelled,
    /// <summary>The item is complete.</summary>
    Completed
}

/// <summary>Provides one minimized, authorized schedule item projection.</summary>
public sealed record CompanionScheduleItemProjection(
    string ItemId,
    string ItemType,
    string Title,
    string? Summary,
    DateOnly LocalDate,
    TimeOnly? StartLocalTime,
    TimeOnly? EndLocalTime,
    string TimeZone,
    CompanionScheduleTimeState TimeState,
    CompanionScheduleOperationalState OperationalState,
    string? PlaceSummary,
    string? TransportationSummary,
    int Sequence,
    bool RequiresAcknowledgment);

/// <summary>Provides the minimized authorized Today and Next application projection.</summary>
public sealed record CompanionTodayProjection(
    CompanionAdventureSummaryProjection Adventure,
    string InformationProfileVersion,
    DateOnly LocalDate,
    string TimeZone,
    CompanionTodayProjectionState State,
    IReadOnlyList<CompanionScheduleItemProjection> TodayItems,
    CompanionScheduleItemProjection? NextItem,
    string? Notice);

/// <summary>Contains the server-established scope for one Itinerary projection.</summary>
public sealed record CompanionItineraryReadScope(
    CreatorId CreatorId,
    UserId UserId,
    string TravelerId,
    long MembershipVersion,
    DateTimeOffset EvaluatedAtUtc);

/// <summary>Provides one ordered, traveler-safe itinerary day projection.</summary>
public sealed record CompanionItineraryDayProjection(
    string ItineraryDayId,
    DateOnly LocalDate,
    string TimeZone,
    int DayNumber,
    string? Title,
    string DestinationVisitId,
    string DestinationName,
    IReadOnlyList<CompanionScheduleItemProjection> Items,
    string? Summary,
    bool HasMaterialChange,
    string? AcknowledgmentId);

/// <summary>Provides the minimized authorized Itinerary application projection.</summary>
public sealed record CompanionItineraryProjection(
    CompanionAdventureSummaryProjection Adventure,
    string InformationProfileVersion,
    IReadOnlyList<CompanionItineraryDayProjection> Days);

/// <summary>Queries authorized Adventure summaries without exposing persistence technology.</summary>
public interface ICompanionAdventureSummaryQuery
{
    /// <summary>Lists authorized Adventures in deterministic order.</summary>
    Task<IReadOnlyList<CompanionAdventureSummaryProjection>> ListAsync(
        CompanionAdventureReadScope scope,
        int maximumResults,
        bool includeCompleted,
        CancellationToken cancellationToken = default);
}

/// <summary>Queries one authorized Adventure detail without revealing unauthorized existence.</summary>
public interface ICompanionAdventureDetailQuery
{
    /// <summary>Gets an authorized Adventure, or <see langword="null"/> for every unavailable case.</summary>
    Task<CompanionAdventureDetailProjection?> GetAsync(
        CompanionAdventureReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Queries one authorized Today projection without revealing unavailable resources.</summary>
public interface ICompanionTodayQuery
{
    /// <summary>Gets Today and Next, or <see langword="null"/> for every unavailable case.</summary>
    Task<CompanionTodayProjection?> GetAsync(
        CompanionTodayReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Queries one authorized Itinerary without revealing unavailable resources.</summary>
public interface ICompanionItineraryQuery
{
    /// <summary>Gets an Itinerary, or <see langword="null"/> for every unavailable case.</summary>
    Task<CompanionItineraryProjection?> GetAsync(
        CompanionItineraryReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default);
}

/// <summary>Keeps Today unavailable until an owning authoritative adapter is implemented.</summary>
public sealed class ClosedCompanionTodayQuery : ICompanionTodayQuery
{
    /// <inheritdoc />
    public Task<CompanionTodayProjection?> GetAsync(
        CompanionTodayReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CompanionTodayProjection?>(null);
}

/// <summary>Keeps Itinerary unavailable until an owning authoritative adapter is implemented.</summary>
public sealed class ClosedCompanionItineraryQuery : ICompanionItineraryQuery
{
    /// <inheritdoc />
    public Task<CompanionItineraryProjection?> GetAsync(
        CompanionItineraryReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CompanionItineraryProjection?>(null);
}
