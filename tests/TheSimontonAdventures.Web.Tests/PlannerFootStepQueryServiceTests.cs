using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies authorized, leakage-safe FootStep discovery and deterministic filtering.</summary>
public sealed class PlannerFootStepQueryServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha");
    private static readonly UserId User = new("user_alpha");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly AdventurePlanId PlanId = new("plan_alpha");
    private static readonly ItineraryDayId DayId = new("day_alpha");

    /// <summary>Combined facets are intersected while values inside each facet are alternatives.</summary>
    [Fact]
    public async Task QueryAsync_CombinedMotorcycleFacets_ReturnsDeterministicPage()
    {
        var source = new RecordingSource(
            Item("b", "Scenic ride", "motorcycle", "outdoors", "scenic", "paved"),
            Item("a", "Direct ride", "motorcycle", "outdoors", "direct", "paved"),
            Item("c", "Walking day", "walking", "culture", "unhurried", "paved"));
        var service = Service(source, Plan());
        var filters = new PlannerFootStepFilters
        {
            TransportationModes = Set("motorcycle"),
            Categories = Set("outdoors"),
            RouteStyles = Set("scenic"),
            Surfaces = Set("paved")
        };

        var result = await service.QueryAsync(Query(filters));

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal("b", Assert.Single(result.Items).Id);
        Assert.Equal(Creator, source.CustomerCreatorId);
    }

    /// <summary>A forged day context is denied before the catalog source is called.</summary>
    [Fact]
    public async Task QueryAsync_ForgedContext_DoesNotDiscloseCatalog()
    {
        var source = new RecordingSource(Item("a", "Private title", "motorcycle", "outdoors", "scenic", "paved"));
        var service = Service(source, Plan());

        var result = await service.QueryAsync(Query(new(), contextId: "day_forged"));

        Assert.False(result.IsAllowed);
        Assert.Empty(result.Items);
        Assert.Equal(0, source.CallCount);
    }

    /// <summary>Denied instance authorization cannot load Planning or catalog data.</summary>
    [Fact]
    public async Task QueryAsync_DeniedAuthorization_DoesNotReadSources()
    {
        var source = new RecordingSource(Item("a", "Private title", "motorcycle", "outdoors", "scenic", "paved"));
        var transactions = new Factory(Plan());
        var service = new PlannerFootStepQueryService(
            new MembershipProvider(Membership()),
            new AuthorizationEvaluator(AuthorizationDecision.Deny(AuthorizationDenialReason.PermissionRequired)),
            transactions, source);

        var result = await service.QueryAsync(Query(new()));

        Assert.False(result.IsAllowed);
        Assert.Equal(0, transactions.BeginCount);
        Assert.Equal(0, source.CallCount);
    }

    /// <summary>Paging is stable after ordinal kind, title, and identity ordering.</summary>
    [Fact]
    public async Task QueryAsync_SecondPage_IsDeterministic()
    {
        var source = new RecordingSource(
            Item("b", "Scenic ride", "motorcycle", "outdoors", "scenic", "paved"),
            Item("c", "Walking day", "walking", "culture", "unhurried", "paved"),
            Item("a", "Direct ride", "motorcycle", "outdoors", "direct", "paved"));
        var query = Query(new()) with { Page = 2 };

        var result = await Service(source, Plan()).QueryAsync(query);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal("c", Assert.Single(result.Items).Id);
    }

    private static PlannerFootStepQueryService Service(RecordingSource source, AdventurePlan plan) => new(
        new MembershipProvider(Membership()),
        new AuthorizationEvaluator(AuthorizationDecision.Allow()),
        new Factory(plan), source);

    private static PlannerFootStepQuery Query(PlannerFootStepFilters filters, string? contextId = null) => new(
        Actor, Creator, PlanId, PlannerFootStepContextKind.Day, contextId ?? DayId.Value,
        "en-US", filters, 1, 2);

    private static PlannerFootStepDefinition Item(
        string id, string title, string mode, string category, string route, string surface) => new()
        {
            Id = id,
            Version = "1.0",
            Kind = "activity",
            Title = title,
            Summary = "Summary",
            Attribution = "Adventures Studio",
            Freshness = "Reviewed",
            ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Day },
            TransportationModes = Set(mode),
            Categories = Set(category),
            RouteStyles = Set(route),
            Surfaces = Set(surface)
        };

    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);

    private static AdventurePlan Plan()
    {
        var visit = new DestinationVisit
        {
            Id = new("visit_alpha"),
            Name = "Example",
            Dates = new(new(2027, 1, 1), new(2027, 1, 3)),
            TimeZone = new("Europe/Rome"),
            Sequence = 1
        };
        var day = new ItineraryDay
        {
            Id = DayId,
            DestinationVisitId = visit.Id,
            Date = new(2027, 1, 2),
            TimeZone = visit.TimeZone,
            Title = "Example day"
        };
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new(PlanId, Creator, "Example", null, AdventureLifecycleStage.Plan,
            PlanningStatus.Draft, new(new(2027, 1, 1), new(2027, 1, 3)),
            new(1, now, now), destinationVisits: [visit], itineraryDays: [day]);
    }

    private static CreatorMembershipSnapshot Membership() => new(
        new("membership_alpha"), User, Creator, CreatorMembershipStatus.Active,
        [CreatorRole.Viewer], [], 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class MembershipProvider(CreatorMembershipSnapshot? membership) : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult(membership);
    }

    private sealed class AuthorizationEvaluator(AuthorizationDecision decision) : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }

    private sealed class RecordingSource(params PlannerFootStepDefinition[] items) : IPlannerFootStepCatalogSource
    {
        public int CallCount { get; private set; }
        public CreatorId CustomerCreatorId { get; private set; }
        public Task<IReadOnlyList<PlannerFootStepDefinition>> ListAsync(CreatorId customerCreatorId, string requestedLocale, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CustomerCreatorId = customerCreatorId;
            return Task.FromResult<IReadOnlyList<PlannerFootStepDefinition>>(items);
        }
    }

    private sealed class Factory(AdventurePlan plan) : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public Task<IPlanningTransaction> BeginAsync(CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.FromResult<IPlanningTransaction>(new Transaction(creatorId, plan));
        }
    }

    private sealed class Transaction(CreatorId creatorId, AdventurePlan plan) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public IAdventurePlanRepository AdventurePlans { get; } = new Repository(plan);
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency => throw new NotSupportedException();
        public IRequiredAuditIntentCollector RequiredAuditIntents => throw new NotSupportedException();
        public Task CommitAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Repository(AdventurePlan plan) : IAdventurePlanRepository
    {
        public Task<AdventurePlan?> GetAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => Task.FromResult<AdventurePlan?>(plan);
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAsync(CreatorId c, AdventurePlan p, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddDestinationVisitAsync(CreatorId c, AdventurePlan p, DestinationVisit i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdatePlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation i, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation i, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
