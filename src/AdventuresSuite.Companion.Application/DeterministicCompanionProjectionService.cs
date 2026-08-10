using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Provides a fixed fictional Companion dataset for contract development and tests only.</summary>
public sealed class DeterministicCompanionProjectionService(
    TimeProvider timeProvider,
    IAuthorizationPolicyEvaluator authorization) : ICompanionProjectionService
{
    /// <summary>Gets the only fictional user identity authorized by the fixture.</summary>
    public const string DemoUserId = "usr_demo_traveler";
    /// <summary>Gets the only fictional traveler identity authorized by the fixture.</summary>
    public const string DemoTravelerId = "trav_demo_primary";
    /// <summary>Gets the only fictional Creator identity authorized by the fixture.</summary>
    public const string DemoCreatorId = "creator_demo_companion";
    /// <summary>Gets the required delegated scope.</summary>
    public const string RequiredScope = "Companion.Access";
    /// <summary>Gets the current fictional Adventure identity.</summary>
    public const string ItalyAdventureId = "adv_demo_italy_2026";

    private const string SchemaVersion = "1.0";
    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionAdventureCollectionDto>> ListAdventuresAsync(
        CompanionAccessContext access, int limit, string? continuationToken, bool includeCompleted,
        string supportId, CancellationToken cancellationToken)
    {
        if (access.IsRevoked
            || access.TravelerId != DemoTravelerId
            || !access.Scopes.Contains(RequiredScope)
            || continuationToken is not null and not "cursor_demo_completed")
        {
            return await Unavailable<CompanionAdventureCollectionDto>();
        }

        var decision = await authorization.AuthorizeAsync(
            new AuthorizationRequest(
                access.Actor,
                Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForCollection(
                    access.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan),
                membershipVersion: access.MembershipVersion),
            cancellationToken);
        if (!decision.IsAllowed)
        {
            return await Unavailable<CompanionAdventureCollectionDto>();
        }

        var now = timeProvider.GetUtcNow();
        var fixtures = AdventureFixtures.All
            .Where(value => includeCompleted || value.Status != CompanionAdventureStatus.Completed)
            .Take(limit)
            .Select(value => CompanionDtoMapper.MapSummary(value, now))
            .ToArray();
        var dto = new CompanionAdventureCollectionDto
        {
            SchemaVersion = SchemaVersion,
            ProjectionVersion = "pv_list_demo_01",
            GeneratedAtUtc = now,
            FreshUntilUtc = now.AddMinutes(15),
            SupportId = supportId,
            Adventures = fixtures,
            ContinuationToken = null
        };
        return await Available(dto);
    }

    private static Task<CompanionQueryResult<T>> Available<T>(T value) where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(value, value.ProjectionVersion));

    private static Task<CompanionQueryResult<T>> Unavailable<T>() where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(null, null));
}

/// <summary>Provides fixed membership facts for the deterministic Test-only vertical slice.</summary>
public sealed class DeterministicCompanionAuthorizationFacts(TimeProvider timeProvider)
    : ICreatorMembershipProvider, IAuthorizationResourceFactsProvider
{
    /// <summary>Gets the fixed current membership version used by the fictional fixture.</summary>
    public const long MembershipVersion = 7;

    /// <inheritdoc />
    public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
        UserId userId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        CreatorMembershipSnapshot? membership =
            userId == new UserId(DeterministicCompanionProjectionService.DemoUserId)
            && creatorId == new CreatorId(DeterministicCompanionProjectionService.DemoCreatorId)
                ? new CreatorMembershipSnapshot(
                    new CreatorMembershipId("membership_demo_companion"),
                    userId,
                    creatorId,
                    CreatorMembershipStatus.Active,
                    [CreatorRole.Viewer],
                    [],
                    MembershipVersion,
                    timeProvider.GetUtcNow().AddDays(-1))
                : null;
        return Task.FromResult(membership);
    }

    /// <inheritdoc />
    public Task<AuthorizationResourceFacts?> GetResourceFactsAsync(
        AuthorizationResourceScope resource,
        CancellationToken cancellationToken = default) => Task.FromResult<AuthorizationResourceFacts?>(null);
}

internal sealed record AdventureFixture(
    string Id, string Title, string? Subtitle, string Description,
    CompanionAdventureStatus Status, DateOnly StartDate, DateOnly EndDate,
    string TimeZone, CompanionOfflineState OfflineState,
    IReadOnlyList<DestinationFixture> Destinations, IReadOnlyList<ScheduleFixture> Items);

internal sealed record DestinationFixture(
    string Id, string Name, DateOnly StartDate, DateOnly EndDate, string TimeZone, int Sequence);

internal sealed record ScheduleFixture(
    string Id, string Type, string Title, string? Summary, DateOnly Date,
    TimeOnly? StartTime, TimeOnly? EndTime, string TimeZone,
    CompanionTimeStatus TimeStatus, CompanionOperationalStatus OperationalStatus,
    string? Place, string? Transportation, int Sequence, bool RequiresAcknowledgment);

internal static class AdventureFixtures
{
    internal static IReadOnlyList<AdventureFixture> All { get; } =
    [
        new(
            DeterministicCompanionProjectionService.ItalyAdventureId,
            "Italian Cities by Rail", "Rome and Florence", "A fictional active journey through two Italian cities.",
            CompanionAdventureStatus.InProgress, new(2026, 8, 9), new(2026, 8, 16), "Europe/Rome",
            CompanionOfflineState.Available,
            [
                new("visit_demo_rome", "Rome", new(2026, 8, 9), new(2026, 8, 12), "Europe/Rome", 1),
                new("visit_demo_florence", "Florence", new(2026, 8, 12), new(2026, 8, 16), "Europe/Rome", 2)
            ],
            [
                new("item_demo_rome_walk", "activity", "Historic Rome walk", "A fictional orientation walk.", new(2026, 8, 10), new(9, 0), new(11, 0), "Europe/Rome", CompanionTimeStatus.Scheduled, CompanionOperationalStatus.Changed, "Central Rome", null, 1, true),
                new("item_demo_rail", "transportation", "Rail to Florence", "A fictional intercity rail segment.", new(2026, 8, 12), new(10, 30), new(12, 5), "Europe/Rome", CompanionTimeStatus.Scheduled, CompanionOperationalStatus.Confirmed, "Roma Termini", "Reserved rail", 2, false),
                new("item_demo_florence_day", "activity", "Florence exploration", null, new(2026, 8, 13), null, null, "Europe/Rome", CompanionTimeStatus.AllDay, CompanionOperationalStatus.Confirmed, "Florence", null, 3, false)
            ]),
        new(
            "adv_demo_phoenix_coast_2027", "Desert to Pacific", "Phoenix to Los Angeles", "A fictional committed domestic journey.",
            CompanionAdventureStatus.Committed, new(2027, 3, 5), new(2027, 3, 9), "America/Phoenix",
            CompanionOfflineState.Available,
            [new("visit_demo_phoenix", "Phoenix", new(2027, 3, 5), new(2027, 3, 6), "America/Phoenix", 1), new("visit_demo_la", "Los Angeles", new(2027, 3, 6), new(2027, 3, 9), "America/Los_Angeles", 2)],
            [new("item_demo_coast", "activity", "Coastal afternoon", null, new(2027, 3, 7), null, null, "America/Los_Angeles", CompanionTimeStatus.Cancelled, CompanionOperationalStatus.Cancelled, "Pacific coast", null, 1, false)]),
        new(
            "adv_demo_spain_2027", "Spain and Atlantic Crossing", "Barcelona to Florida", "A fictional planned journey.",
            CompanionAdventureStatus.Planned, new(2027, 10, 25), new(2027, 11, 15), "Europe/Madrid",
            CompanionOfflineState.Available,
            [new("visit_demo_barcelona", "Barcelona", new(2027, 10, 25), new(2027, 10, 29), "Europe/Madrid", 1)],
            [new("item_demo_spain_activity", "activity", "Barcelona activity", null, new(2027, 10, 27), null, null, "Europe/Madrid", CompanionTimeStatus.ToBeConfirmed, CompanionOperationalStatus.Proposed, "Barcelona", null, 1, false)]),
        new(
            "adv_demo_completed_2025", "Completed Demo Journey", null, "A fictional completed history fixture.",
            CompanionAdventureStatus.Completed, new(2025, 5, 1), new(2025, 5, 4), "America/Phoenix",
            CompanionOfflineState.Expired, [], [])
    ];
}
