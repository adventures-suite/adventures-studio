using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Contains the authenticated, server-derived facts required by a Companion query.</summary>
public sealed record CompanionAccessContext(
    ActorIdentity Actor,
    string TravelerId,
    CreatorId CreatorId,
    long MembershipVersion,
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

    /// <summary>Gets one authorized traveler-safe Adventure overview.</summary>
    Task<CompanionQueryResult<CompanionAdventureDto>> GetAdventureAsync(
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken);

    /// <summary>Gets one authorized traveler-specific Today and Next projection.</summary>
    Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken);
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
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken) => Unavailable<CompanionAdventureDto>();

    /// <inheritdoc />
    public Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken) => Unavailable<CompanionTodayDto>();

    private static Task<CompanionQueryResult<T>> Unavailable<T>() where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(null, null));
}
