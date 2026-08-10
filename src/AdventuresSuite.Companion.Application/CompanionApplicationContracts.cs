using AdventuresSuite.Companion.Contracts;

namespace AdventuresSuite.Companion.Application;

/// <summary>Contains the authenticated, server-derived facts required by a Companion query.</summary>
public sealed record CompanionAccessContext(
    string UserId,
    string TravelerId,
    string CreatorId,
    bool IsRevoked,
    IReadOnlySet<string> Scopes);

/// <summary>Represents an enumeration-safe application query result.</summary>
/// <typeparam name="T">The authorized DTO type.</typeparam>
public sealed record CompanionQueryResult<T>(T? Value, string? ProjectionVersion)
    where T : CompanionProjectionDto
{
    /// <summary>Gets whether an authorized projection is available.</summary>
    public bool IsAvailable => Value is not null;
}

/// <summary>Queries authorized, purpose-built Companion projections.</summary>
public interface ICompanionProjectionService
{
    /// <summary>Lists visible Adventures using bounded pagination.</summary>
    Task<CompanionQueryResult<CompanionAdventureCollectionDto>> ListAdventuresAsync(
        CompanionAccessContext access, int limit, string? continuationToken, bool includeCompleted,
        string supportId, CancellationToken cancellationToken);

    /// <summary>Gets one traveler-safe Adventure overview.</summary>
    Task<CompanionQueryResult<CompanionAdventureDto>> GetAdventureAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken);

    /// <summary>Gets Today and Next.</summary>
    Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken);

    /// <summary>Gets the traveler-safe itinerary.</summary>
    Task<CompanionQueryResult<CompanionItineraryDto>> GetItineraryAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken);

    /// <summary>Gets traveler-visible readiness.</summary>
    Task<CompanionQueryResult<CompanionReadinessDto>> GetReadinessAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken);

    /// <summary>Gets the structured traveler Playbook.</summary>
    Task<CompanionQueryResult<CompanionPlaybookDto>> GetPlaybookAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken);
}

/// <summary>Provides fail-closed projections when deterministic fixtures are not explicitly enabled.</summary>
public sealed class ClosedCompanionProjectionService : ICompanionProjectionService
{
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionAdventureCollectionDto>> ListAdventuresAsync(
        CompanionAccessContext access, int limit, string? continuationToken, bool includeCompleted,
        string supportId, CancellationToken cancellationToken) => Unavailable<CompanionAdventureCollectionDto>();
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionAdventureDto>> GetAdventureAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken) => Unavailable<CompanionAdventureDto>();
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken) => Unavailable<CompanionTodayDto>();
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionItineraryDto>> GetItineraryAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken) => Unavailable<CompanionItineraryDto>();
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionReadinessDto>> GetReadinessAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken) => Unavailable<CompanionReadinessDto>();
    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionPlaybookDto>> GetPlaybookAsync(
        CompanionAccessContext access, string adventureId, string supportId, CancellationToken cancellationToken) => Unavailable<CompanionPlaybookDto>();

    private static Task<CompanionQueryResult<T>> Unavailable<T>() where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(null, null));
}
