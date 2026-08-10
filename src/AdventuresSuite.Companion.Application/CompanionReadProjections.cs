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
