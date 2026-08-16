using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the Planner read slice cannot reach persistence before authorization.</summary>
public sealed class PlannerWorkspaceQueryServiceTests
{
    private static readonly UserId User = new("user_planner_01");
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);

    /// <summary>Denied collection access returns no plans and never opens Planning persistence.</summary>
    [Fact]
    public async Task ListAsync_DeniedAuthorization_DoesNotBeginPlanningTransaction()
    {
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Deny(
                AuthorizationDenialReason.PermissionRequired)),
            transactions);

        var result = await service.ListAsync(Actor, Creator);

        Assert.False(result.IsAllowed);
        Assert.Empty(result.Plans);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>Allowed collection access lists plans through the explicit Creator-bound transaction.</summary>
    [Fact]
    public async Task ListAsync_AllowedAuthorization_UsesCreatorScopedRead()
    {
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Allow()),
            transactions);

        var result = await service.ListAsync(Actor, Creator);

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Plans);
        Assert.Equal(1, transactions.BeginCount);
        Assert.Equal(Creator, transactions.LastCreatorId);
    }

    /// <summary>An absent membership denies without evaluating or reading Planning state.</summary>
    [Fact]
    public async Task ListAsync_AbsentMembership_DoesNotAuthorizeOrRead()
    {
        var authorization = new RecordingAuthorizationEvaluator();
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(null), authorization, transactions);

        var result = await service.ListAsync(Actor, Creator);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>A forged Creator identity is used only as an authorization scope and never broadens reads.</summary>
    [Fact]
    public async Task ListAsync_ForgedCreatorId_CannotReadAuthorizedCreatorPlans()
    {
        var forged = new CreatorId("creator_forged_01");
        var membershipProvider = new RecordingMembershipProvider();
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            membershipProvider,
            new StubAuthorizationEvaluator(AuthorizationDecision.Allow()),
            transactions);

        var result = await service.ListAsync(Actor, forged);

        Assert.False(result.IsAllowed);
        Assert.Equal(forged, membershipProvider.LastCreatorId);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>Revoked membership is re-evaluated below the UI and cannot reach Planning.</summary>
    [Fact]
    public async Task ListAsync_RevokedMembership_DoesNotReadPlanning()
    {
        var membership = Membership(CreatorMembershipStatus.Revoked);
        var provider = new StubMembershipProvider(membership);
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            provider,
            Policy(provider),
            transactions);

        var result = await service.ListAsync(Actor, Creator);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>A membership version change between context load and policy evaluation fails closed.</summary>
    [Fact]
    public async Task ListAsync_StaleMembershipVersion_DoesNotReadPlanning()
    {
        var provider = new SequencedMembershipProvider(
            Membership(version: 3), Membership(version: 4));
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            provider,
            Policy(provider),
            transactions);

        var result = await service.ListAsync(Actor, Creator);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>Denied instance access cannot read the allowlisted detail projection.</summary>
    [Fact]
    public async Task GetAsync_DeniedInstanceAuthorization_DoesNotReadPlanning()
    {
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Deny(
                AuthorizationDenialReason.ResourceScopeMismatch)),
            transactions);

        var result = await service.GetAsync(Actor, Creator, new("plan_spain_2027"));

        Assert.False(result.IsAllowed);
        Assert.Null(result.Plan);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>Allowed instance access uses the explicit Creator and plan identities.</summary>
    [Fact]
    public async Task GetAsync_AllowedInstanceAuthorization_ReadsAllowlistedDetail()
    {
        var detail = Detail();
        var transactions = new StubPlanningTransactionFactory(detail);
        var authorization = new RecordingAllowedAuthorizationEvaluator();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()), authorization, transactions);

        var result = await service.GetAsync(Actor, Creator, detail.Id);

        Assert.True(result.IsAllowed);
        Assert.Same(detail, result.Plan);
        Assert.Equal(AuthorizationResourceScopeType.ResourceInstance,
            authorization.LastRequest!.Resource.ScopeType);
        Assert.Equal(detail.Id.Value, authorization.LastRequest.Resource.ResourceId);
        Assert.Equal(Creator, transactions.LastCreatorId);
    }

    /// <summary>Edit form visibility requires a separate required-mutation instance decision.</summary>
    [Fact]
    public async Task GetAsync_EditCapability_RequiresRequiredMutationDecision()
    {
        var detail = Detail();
        var allowed = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new SequencedAuthorizationEvaluator(
                AuthorizationDecision.Allow(),
                AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
            new StubPlanningTransactionFactory(detail));
        var viewer = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new SequencedAuthorizationEvaluator(
                AuthorizationDecision.Allow(),
                AuthorizationDecision.Deny(AuthorizationDenialReason.PermissionRequired)),
            new StubPlanningTransactionFactory(detail));

        Assert.True((await allowed.GetAsync(Actor, Creator, detail.Id)).CanEdit);
        Assert.False((await viewer.GetAsync(Actor, Creator, detail.Id)).CanEdit);
    }

    /// <summary>An absent instance membership cannot authorize or reach Planning persistence.</summary>
    [Fact]
    public async Task GetAsync_AbsentMembership_DoesNotAuthorizeOrRead()
    {
        var authorization = new RecordingAuthorizationEvaluator();
        var transactions = new StubPlanningTransactionFactory();
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(null), authorization, transactions);

        var result = await service.GetAsync(Actor, Creator, new("plan_spain_2027"));

        Assert.False(result.IsAllowed);
        Assert.Null(result.Plan);
        Assert.Equal(0, authorization.CallCount);
        Assert.Equal(0, transactions.BeginCount);
    }

    /// <summary>A mismatched persistence projection fails closed after instance authorization.</summary>
    [Fact]
    public async Task GetAsync_MismatchedPlanProjection_IsDenied()
    {
        var mismatched = Detail() with { Id = new("plan_other_2027") };
        var transactions = new StubPlanningTransactionFactory(mismatched);
        var service = new PlannerWorkspaceQueryService(
            new StubMembershipProvider(Membership()),
            new StubAuthorizationEvaluator(AuthorizationDecision.Allow()),
            transactions);

        var result = await service.GetAsync(Actor, Creator, new("plan_spain_2027"));

        Assert.False(result.IsAllowed);
        Assert.Null(result.Plan);
    }

    private static AdventurePlanDetail Detail() => new()
    {
        Id = new("plan_spain_2027"),
        Title = "Spain and Atlantic",
        LifecycleStage = AdventureLifecycleStage.Plan,
        Status = PlanningStatus.Planned,
        Dates = new(new(2027, 10, 25), new(2027, 11, 15)),
        Version = 7,
        TravelerCount = 2
    };

    private static CreatorMembershipSnapshot Membership(
        CreatorMembershipStatus status = CreatorMembershipStatus.Active,
        long version = 3) => new(
        new CreatorMembershipId("membership_planner_01"), User, Creator,
        status, [CreatorRole.Viewer], [], version,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static AuthorizationPolicyEvaluator Policy(ICreatorMembershipProvider provider) =>
        new(provider, new UnusedResourceFactsProvider(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
    }

    private sealed class StubAuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(decision);
    }

    private sealed class RecordingAuthorizationEvaluator : IAuthorizationPolicyEvaluator
    {
        public int CallCount { get; private set; }

        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(AuthorizationDecision.Deny(
                AuthorizationDenialReason.MembershipRequired));
        }
    }

    private sealed class RecordingAllowedAuthorizationEvaluator : IAuthorizationPolicyEvaluator
    {
        public AuthorizationRequest? LastRequest { get; private set; }

        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(AuthorizationDecision.Allow());
        }
    }

    private sealed class RecordingMembershipProvider : ICreatorMembershipProvider
    {
        public CreatorId LastCreatorId { get; private set; }

        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            LastCreatorId = creatorId;
            return Task.FromResult<CreatorMembershipSnapshot?>(null);
        }
    }

    private sealed class SequencedMembershipProvider(params CreatorMembershipSnapshot[] memberships)
        : ICreatorMembershipProvider
    {
        private int index;

        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            var membership = memberships[Math.Min(index, memberships.Length - 1)];
            index++;
            return Task.FromResult<CreatorMembershipSnapshot?>(membership);
        }
    }

    private sealed class SequencedAuthorizationEvaluator(params AuthorizationDecision[] decisions)
        : IAuthorizationPolicyEvaluator
    {
        private int index;
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(decisions[Math.Min(index++, decisions.Length - 1)]);
    }

    private sealed class UnusedResourceFactsProvider : IAuthorizationResourceFactsProvider
    {
        public Task<AuthorizationResourceFacts?> GetResourceFactsAsync(
            AuthorizationResourceScope resource,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Collection authorization must not load instance facts.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StubPlanningTransactionFactory(AdventurePlanDetail? detail = null) : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public CreatorId LastCreatorId { get; private set; }

        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            BeginCount++;
            LastCreatorId = creatorId;
            return Task.FromResult<IPlanningTransaction>(new StubPlanningTransaction(creatorId, detail));
        }
    }

    private sealed class StubPlanningTransaction(CreatorId creatorId, AdventurePlanDetail? detail) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public IAdventurePlanRepository AdventurePlans { get; } = new EmptyAdventurePlanRepository(detail);
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency { get; } =
            new UnusedIdempotencyStore();
        public IRequiredAuditIntentCollector RequiredAuditIntents { get; } = new UnusedAuditIntentCollector();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class UnusedIdempotencyStore : IAdventurePlanCreateIdempotencyStore
    {
        public Task<AdventurePlanCreateIdempotencyResult> ReserveAsync(
            CreatorId creatorId,
            AdventurePlanCreateReservation reservation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Read-only queries cannot reserve idempotency results.");
    }

    private sealed class UnusedAuditIntentCollector : IRequiredAuditIntentCollector
    {
        public void AddRequired(AuditEventIntent auditEvent) =>
            throw new InvalidOperationException("Read-only queries cannot collect mutation audit intent.");
    }

    private sealed class EmptyAdventurePlanRepository(AdventurePlanDetail? detail) : IAdventurePlanRepository
    {
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => Task.FromResult<AdventurePlanAuthorizationFacts?>(null);
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlanDashboardItem>>([]);
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => Task.FromResult(detail);
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => Task.FromResult<AdventurePlan?>(null);
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlan>>([]);
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdventurePlan>>([]);
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan, DestinationVisit destinationVisit, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan, ItineraryDay itineraryDay, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan, PlannedActivity activity, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdatePlannedActivityAsync(CreatorId creatorId, AdventurePlan plan, PlannedActivity activity, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan, TransportationSegment segment, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan, TransportationSegment segment, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId creatorId, AdventurePlan plan, Accommodation accommodation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAccommodationAsync(CreatorId creatorId, AdventurePlan plan, Accommodation accommodation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId creatorId, AdventurePlan plan, Reservation reservation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
