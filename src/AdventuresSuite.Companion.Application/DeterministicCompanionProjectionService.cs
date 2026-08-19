using AdventuresSuite.Companion.Contracts;
using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;

namespace AdventuresSuite.Companion.Application;

/// <summary>Provides a fixed fictional Companion dataset for contract development and tests only.</summary>
public sealed class DeterministicCompanionProjectionService(
    TimeProvider timeProvider,
    IAuthorizationPolicyEvaluator authorization,
    ICompanionTodayQuery today,
    ICompanionItineraryQuery itinerary) : ICompanionProjectionService
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

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionAdventureDto>> GetAdventureAsync(
        CompanionAccessContext access, string adventureId, string supportId,
        CancellationToken cancellationToken)
    {
        if (access.IsRevoked || !access.Scopes.Contains(RequiredScope))
            return await Unavailable<CompanionAdventureDto>();

        var source = AdventureFixtures.All.FirstOrDefault(value =>
            string.Equals(value.Id, adventureId, StringComparison.Ordinal));
        if (source is null
            || source.CreatorId != access.CreatorId.Value
            || source.TravelerId != access.TravelerId)
        {
            return await Unavailable<CompanionAdventureDto>();
        }

        var decision = await authorization.AuthorizeAsync(
            new AuthorizationRequest(
                access.Actor,
                Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForInstance(
                    new CreatorId(source.CreatorId),
                    AuthorizationResourceTypes.AdventurePlan,
                    source.Id),
                membershipVersion: access.MembershipVersion),
            cancellationToken);
        if (!decision.IsAllowed)
            return await Unavailable<CompanionAdventureDto>();

        return await Available(CompanionDtoMapper.MapAdventure(source, timeProvider.GetUtcNow(), supportId));
    }

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionTodayDto>> GetTodayAsync(
        CompanionAccessContext access,
        string adventureId,
        string supportId,
        CancellationToken cancellationToken)
    {
        if (access.IsRevoked
            || !access.Actor.UserId.HasValue
            || access.MembershipVersion < 1
            || !access.Scopes.Contains(RequiredScope))
        {
            return await Unavailable<CompanionTodayDto>();
        }

        var now = timeProvider.GetUtcNow();
        var source = await today.GetAsync(
            new CompanionTodayReadScope(
                access.CreatorId,
                access.Actor.UserId.Value,
                access.TravelerId,
                access.MembershipVersion,
                now),
            adventureId,
            cancellationToken);
        if (source is null)
            return await Unavailable<CompanionTodayDto>();

        var decision = await authorization.AuthorizeAsync(
            new AuthorizationRequest(
                access.Actor,
                Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForInstance(
                    access.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan,
                    source.Adventure.AdventureId),
                membershipVersion: access.MembershipVersion),
            cancellationToken);
        if (!decision.IsAllowed
            || !CompanionDtoMapper.TryMapToday(
                source, access.CreatorId.Value, access.TravelerId, adventureId, now, supportId, out var dto))
        {
            return await Unavailable<CompanionTodayDto>();
        }

        return await Available(dto!);
    }

    /// <inheritdoc />
    public async Task<CompanionQueryResult<CompanionItineraryDto>> GetItineraryAsync(
        CompanionAccessContext access,
        string adventureId,
        string supportId,
        CancellationToken cancellationToken)
    {
        if (access.IsRevoked
            || !access.Actor.UserId.HasValue
            || access.MembershipVersion < 1
            || !access.Scopes.Contains(RequiredScope))
        {
            return await Unavailable<CompanionItineraryDto>();
        }

        var now = timeProvider.GetUtcNow();
        var source = await itinerary.GetAsync(
            new CompanionItineraryReadScope(
                access.CreatorId,
                access.Actor.UserId.Value,
                access.TravelerId,
                access.MembershipVersion,
                now),
            adventureId,
            cancellationToken);
        if (source is null)
            return await Unavailable<CompanionItineraryDto>();

        var decision = await authorization.AuthorizeAsync(
            new AuthorizationRequest(
                access.Actor,
                Permissions.AdventurePlanView,
                AuthorizationResourceScope.ForInstance(
                    access.CreatorId,
                    AuthorizationResourceTypes.AdventurePlan,
                    source.Adventure.AdventureId),
                membershipVersion: access.MembershipVersion),
            cancellationToken);
        if (!decision.IsAllowed
            || !CompanionDtoMapper.TryMapItinerary(
                source, access.CreatorId.Value, access.TravelerId, adventureId, now, supportId, out var dto))
        {
            return await Unavailable<CompanionItineraryDto>();
        }

        return await Available(dto!);
    }

    private static Task<CompanionQueryResult<T>> Available<T>(T value) where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(value, value.ProjectionVersion));

    private static Task<CompanionQueryResult<T>> Unavailable<T>() where T : CompanionProjectionDto =>
        Task.FromResult(new CompanionQueryResult<T>(null, null));
}

/// <summary>Provides deterministic fictional Today data only for the explicitly enabled Test host.</summary>
public sealed class DeterministicCompanionTodayQuery : ICompanionTodayQuery
{
    /// <inheritdoc />
    public Task<CompanionTodayProjection?> GetAsync(
        CompanionTodayReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope.UserId != new UserId(DeterministicCompanionProjectionService.DemoUserId)
            || scope.MembershipVersion != DeterministicCompanionAuthorizationFacts.MembershipVersion)
        {
            return Task.FromResult<CompanionTodayProjection?>(null);
        }

        var source = AdventureFixtures.All.FirstOrDefault(value =>
            string.Equals(value.Id, adventureId, StringComparison.Ordinal)
            && string.Equals(value.CreatorId, scope.CreatorId.Value, StringComparison.Ordinal)
            && string.Equals(value.TravelerId, scope.TravelerId, StringComparison.Ordinal));
        if (source is null)
            return Task.FromResult<CompanionTodayProjection?>(null);

        if (!TryGetLocalDate(scope.EvaluatedAtUtc, source.TimeZone, out var localDate))
            return Task.FromResult<CompanionTodayProjection?>(null);

        var todayItems = source.Items
            .Where(value => value.Date == localDate)
            .OrderBy(value => value.Sequence)
            .Select(MapScheduleItem)
            .ToArray();
        var next = source.Items
            .Where(value => value.Date > localDate)
            .OrderBy(value => value.Date)
            .ThenBy(value => value.Sequence)
            .FirstOrDefault();
        var state = localDate < source.StartDate
            ? CompanionTodayProjectionState.BeforeAdventure
            : localDate > source.EndDate
                ? CompanionTodayProjectionState.AfterAdventure
                : todayItems.Length == 0
                    ? CompanionTodayProjectionState.NoScheduledItems
                    : CompanionTodayProjectionState.Active;
        var adventure = new CompanionAdventureSummaryProjection(
            source.Id,
            source.TravelerId,
            source.Title,
            MapLifecycle(source.Status),
            source.StartDate,
            source.EndDate,
            source.TimeZone,
            PlanVersion: 7,
            ParticipationVersion: 3,
            UpdatedAtUtc: new DateTimeOffset(2026, 8, 9, 18, 0, 0, TimeSpan.Zero));
        CompanionTodayProjection? result = new(
            adventure,
            "info_demo_01",
            localDate,
            source.TimeZone,
            state,
            todayItems,
            next is null ? null : MapScheduleItem(next),
            "Times are shown in the Adventure's local time zone.");
        return Task.FromResult<CompanionTodayProjection?>(result);
    }

    private static CompanionScheduleItemProjection MapScheduleItem(ScheduleFixture source) => new(
        source.Id,
        source.Type,
        source.Title,
        source.Summary,
        source.Date,
        source.StartTime,
        source.EndTime,
        source.TimeZone,
        source.TimeStatus switch
        {
            CompanionTimeStatus.Scheduled => CompanionScheduleTimeState.Scheduled,
            CompanionTimeStatus.AllDay => CompanionScheduleTimeState.AllDay,
            CompanionTimeStatus.ToBeConfirmed => CompanionScheduleTimeState.ToBeConfirmed,
            CompanionTimeStatus.Cancelled => CompanionScheduleTimeState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        },
        source.OperationalStatus switch
        {
            CompanionOperationalStatus.Proposed => CompanionScheduleOperationalState.Proposed,
            CompanionOperationalStatus.Reserved => CompanionScheduleOperationalState.Reserved,
            CompanionOperationalStatus.Confirmed => CompanionScheduleOperationalState.Confirmed,
            CompanionOperationalStatus.Changed => CompanionScheduleOperationalState.Changed,
            CompanionOperationalStatus.Cancelled => CompanionScheduleOperationalState.Cancelled,
            CompanionOperationalStatus.Completed => CompanionScheduleOperationalState.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        },
        source.Place,
        source.Transportation,
        source.Sequence,
        source.RequiresAcknowledgment);

    private static CompanionAdventureLifecycle MapLifecycle(CompanionAdventureStatus status) => status switch
    {
        CompanionAdventureStatus.Planned => CompanionAdventureLifecycle.Planned,
        CompanionAdventureStatus.Committed => CompanionAdventureLifecycle.Committed,
        CompanionAdventureStatus.InProgress => CompanionAdventureLifecycle.InProgress,
        CompanionAdventureStatus.Completed => CompanionAdventureLifecycle.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static bool TryGetLocalDate(DateTimeOffset utc, string timeZone, out DateOnly result)
    {
        result = default;
        try
        {
            result = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

/// <summary>Provides deterministic fictional Itinerary data only for the explicitly enabled Test host.</summary>
public sealed class DeterministicCompanionItineraryQuery : ICompanionItineraryQuery
{
    /// <inheritdoc />
    public Task<CompanionItineraryProjection?> GetAsync(
        CompanionItineraryReadScope scope,
        string adventureId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scope.UserId != new UserId(DeterministicCompanionProjectionService.DemoUserId)
            || scope.MembershipVersion != DeterministicCompanionAuthorizationFacts.MembershipVersion)
        {
            return Task.FromResult<CompanionItineraryProjection?>(null);
        }

        var source = AdventureFixtures.All.FirstOrDefault(value =>
            string.Equals(value.Id, adventureId, StringComparison.Ordinal)
            && string.Equals(value.CreatorId, scope.CreatorId.Value, StringComparison.Ordinal)
            && string.Equals(value.TravelerId, scope.TravelerId, StringComparison.Ordinal));
        if (source is null)
            return Task.FromResult<CompanionItineraryProjection?>(null);

        var adventure = new CompanionAdventureSummaryProjection(
            source.Id, source.TravelerId, source.Title, MapLifecycle(source.Status),
            source.StartDate, source.EndDate, source.TimeZone, 7, 3,
            new DateTimeOffset(2026, 8, 9, 18, 0, 0, TimeSpan.Zero));
        var days = source.Items.GroupBy(value => value.Date).OrderBy(value => value.Key)
            .Select((group, index) =>
            {
                var destination = source.Destinations.FirstOrDefault(value =>
                    group.Key >= value.StartDate && group.Key <= value.EndDate);
                if (destination is null) return null;
                var items = group.OrderBy(value => value.Sequence).Select(MapScheduleItem).ToArray();
                var changed = items.Any(value => value.RequiresAcknowledgment);
                return new CompanionItineraryDayProjection(
                    $"day_{source.Id}_{index + 1}", group.Key, destination.TimeZone, index + 1,
                    destination.Name, destination.Id, destination.Name, items, null, changed,
                    changed ? $"ack_{source.Id}_{index + 1}" : null);
            }).ToArray();
        if (days.Any(value => value is null))
            return Task.FromResult<CompanionItineraryProjection?>(null);

        return Task.FromResult<CompanionItineraryProjection?>(new(
            adventure, "info_demo_01", days.Select(value => value!).ToArray()));
    }

    private static CompanionScheduleItemProjection MapScheduleItem(ScheduleFixture source) => new(
        source.Id, source.Type, source.Title, source.Summary, source.Date, source.StartTime, source.EndTime,
        source.TimeZone, source.TimeStatus switch
        {
            CompanionTimeStatus.Scheduled => CompanionScheduleTimeState.Scheduled,
            CompanionTimeStatus.AllDay => CompanionScheduleTimeState.AllDay,
            CompanionTimeStatus.ToBeConfirmed => CompanionScheduleTimeState.ToBeConfirmed,
            CompanionTimeStatus.Cancelled => CompanionScheduleTimeState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        }, source.OperationalStatus switch
        {
            CompanionOperationalStatus.Proposed => CompanionScheduleOperationalState.Proposed,
            CompanionOperationalStatus.Reserved => CompanionScheduleOperationalState.Reserved,
            CompanionOperationalStatus.Confirmed => CompanionScheduleOperationalState.Confirmed,
            CompanionOperationalStatus.Changed => CompanionScheduleOperationalState.Changed,
            CompanionOperationalStatus.Cancelled => CompanionScheduleOperationalState.Cancelled,
            CompanionOperationalStatus.Completed => CompanionScheduleOperationalState.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        }, source.Place, source.Transportation,
        source.Sequence, source.RequiresAcknowledgment);

    private static CompanionAdventureLifecycle MapLifecycle(CompanionAdventureStatus status) => status switch
    {
        CompanionAdventureStatus.Planned => CompanionAdventureLifecycle.Planned,
        CompanionAdventureStatus.Committed => CompanionAdventureLifecycle.Committed,
        CompanionAdventureStatus.InProgress => CompanionAdventureLifecycle.InProgress,
        CompanionAdventureStatus.Completed => CompanionAdventureLifecycle.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
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
        CancellationToken cancellationToken = default)
    {
        var source = AdventureFixtures.All.FirstOrDefault(value =>
            resource.ScopeType == AuthorizationResourceScopeType.ResourceInstance
            && resource.ResourceType == AuthorizationResourceTypes.AdventurePlan
            && string.Equals(value.Id, resource.ResourceId, StringComparison.Ordinal));
        AuthorizationResourceFacts? facts = source is null
            ? null
            : new AuthorizationResourceFacts(
                new CreatorId(source.CreatorId),
                AuthorizationResourceTypes.AdventurePlan,
                source.Id,
                source.Status == CompanionAdventureStatus.Completed,
                version: 1);
        return Task.FromResult(facts);
    }
}

internal sealed record AdventureFixture(
    string Id, string CreatorId, string TravelerId,
    string Title, string? Subtitle, string Description,
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
            DeterministicCompanionProjectionService.DemoCreatorId,
            DeterministicCompanionProjectionService.DemoTravelerId,
            "Italian Cities by Rail", "Rome and Florence", "A fictional active journey through two Italian cities.",
            CompanionAdventureStatus.InProgress, new(2026, 8, 9), new(2026, 8, 16), "Europe/Rome",
            CompanionOfflineState.Available,
            [
                new("visit_demo_rome", "Rome", new(2026, 8, 9), new(2026, 8, 12), "Europe/Rome", 1),
                new("visit_demo_florence", "Florence", new(2026, 8, 12), new(2026, 8, 16), "Europe/Rome", 2)
            ],
            [
                new("item_demo_rome_walk", "activity", "Historic Rome walk", "A fictional orientation walk.", new(2026, 8, 10), new(9, 0), new(11, 0), "Europe/Rome", CompanionTimeStatus.Scheduled, CompanionOperationalStatus.Changed, "Central Rome", null, 1, true),
                new("item_demo_rome_day", "activity", "Rome orientation day", "A fictional all-day orientation.", new(2026, 8, 10), null, null, "Europe/Rome", CompanionTimeStatus.AllDay, CompanionOperationalStatus.Confirmed, "Rome", null, 2, false),
                new("item_demo_rail", "transportation", "Rail to Florence", "A fictional intercity rail segment.", new(2026, 8, 12), new(10, 30), new(12, 5), "Europe/Rome", CompanionTimeStatus.Scheduled, CompanionOperationalStatus.Confirmed, "Roma Termini", "Rail segment", 2, false),
                new("item_demo_florence_day", "activity", "Florence exploration", null, new(2026, 8, 13), null, null, "Europe/Rome", CompanionTimeStatus.AllDay, CompanionOperationalStatus.Confirmed, "Florence", null, 3, false)
            ]),
        new(
            "adv_demo_phoenix_coast_2027", DeterministicCompanionProjectionService.DemoCreatorId,
            DeterministicCompanionProjectionService.DemoTravelerId,
            "Desert to Pacific", "Phoenix to Los Angeles", "A fictional committed domestic journey.",
            CompanionAdventureStatus.Committed, new(2027, 3, 5), new(2027, 3, 9), "America/Phoenix",
            CompanionOfflineState.Available,
            [new("visit_demo_phoenix", "Phoenix", new(2027, 3, 5), new(2027, 3, 6), "America/Phoenix", 1), new("visit_demo_la", "Los Angeles", new(2027, 3, 6), new(2027, 3, 9), "America/Los_Angeles", 2)],
            [new("item_demo_coast", "activity", "Coastal afternoon", null, new(2027, 3, 7), null, null, "America/Los_Angeles", CompanionTimeStatus.Cancelled, CompanionOperationalStatus.Cancelled, "Pacific coast", null, 1, false)]),
        new(
            "adv_demo_spain_2027", DeterministicCompanionProjectionService.DemoCreatorId,
            DeterministicCompanionProjectionService.DemoTravelerId,
            "Spain and Atlantic Crossing", "Barcelona to Florida", "A fictional planned journey.",
            CompanionAdventureStatus.Planned, new(2027, 10, 25), new(2027, 11, 15), "Europe/Madrid",
            CompanionOfflineState.Available,
            [new("visit_demo_barcelona", "Barcelona", new(2027, 10, 25), new(2027, 10, 29), "Europe/Madrid", 1)],
            [new("item_demo_spain_activity", "activity", "Barcelona activity", null, new(2027, 10, 27), null, null, "Europe/Madrid", CompanionTimeStatus.ToBeConfirmed, CompanionOperationalStatus.Proposed, "Barcelona", null, 1, false)]),
        new(
            "adv_demo_completed_2025", DeterministicCompanionProjectionService.DemoCreatorId,
            DeterministicCompanionProjectionService.DemoTravelerId,
            "Completed Demo Journey", null, "A fictional completed history fixture.",
            CompanionAdventureStatus.Completed, new(2025, 5, 1), new(2025, 5, 4), "America/Phoenix",
            CompanionOfflineState.Expired, [], [])
    ];
}
