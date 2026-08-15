using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the authorized, audited, retry-safe manual creation boundary.</summary>
public sealed class ManualAdventurePlanCreateServiceTests
{
    private static readonly UserId User = new("user_planner_01");
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A valid request commits only a version-one private draft and its required audit.</summary>
    [Fact]
    public async Task CreateAsync_ValidRequest_CommitsPlanIdempotencyAndAuditTogether()
    {
        var transaction = new RecordingTransaction(Creator);
        var authorization = new RecordingAuthorizationEvaluator(Allowed());
        var service = Service(transaction, authorization);

        var result = await service.CreateAsync(Command());

        Assert.Equal(ManualAdventurePlanCreateOutcome.Created, result.Outcome);
        Assert.Equal(new AdventurePlanId("plan_created_01"), result.AdventurePlanId);
        Assert.True(transaction.Committed);
        var plan = Assert.IsType<AdventurePlan>(transaction.Repository.Added);
        Assert.Equal(Creator, plan.CreatorId);
        Assert.Equal("Desert weekend", plan.Title);
        Assert.Equal("Private draft", plan.WorkingDescription);
        Assert.Equal(AdventureLifecycleStage.Plan, plan.LifecycleStage);
        Assert.Equal(PlanningStatus.Draft, plan.Status);
        Assert.Equal(1, plan.Audit.Version);
        Assert.Equal(Now, plan.Audit.CreatedAtUtc);
        Assert.Empty(plan.Travelers);
        Assert.Empty(plan.DestinationVisits);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(Actor, audit.Actor);
        Assert.Equal(Permissions.AdventurePlanCreate, audit.Permission);
        Assert.Equal(AuthorizationResourceScopeType.ResourceInstance, audit.Resource.ScopeType);
        Assert.Equal(plan.Id.Value, audit.Resource.ResourceId);
        Assert.Equal(1, audit.ResultingVersion);
        Assert.Equal(Now, audit.OccurredAtUtc);
        Assert.Equal(AuthorizationResourceScopeType.CreatorCollection,
            authorization.LastRequest!.Resource.ScopeType);
        Assert.Equal(Permissions.AdventurePlanCreate, authorization.LastRequest.Permission);
    }

    /// <summary>Authorization denial occurs before any Planning transaction or key lookup.</summary>
    [Fact]
    public async Task CreateAsync_Denied_DoesNotReachPersistence()
    {
        var factory = new RecordingTransactionFactory(new RecordingTransaction(Creator));
        var service = new ManualAdventurePlanCreateService(
            Membership(),
            new RecordingAuthorizationEvaluator(AuthorizationDecision.Deny(
                AuthorizationDenialReason.PermissionRequired)),
            factory,
            new FixedIdentities(),
            new FixedTimeProvider());

        var result = await service.CreateAsync(Command());

        Assert.Equal(ManualAdventurePlanCreateOutcome.Denied, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>A Creator without membership cannot probe another Creator's durable key.</summary>
    [Fact]
    public async Task CreateAsync_CrossCreatorRequest_DoesNotAuthorizeOrProbeIdempotency()
    {
        var authorization = new RecordingAuthorizationEvaluator(Allowed());
        var factory = new RecordingTransactionFactory(new RecordingTransaction(Creator));
        var service = new ManualAdventurePlanCreateService(
            new StubMembershipProvider(null), authorization, factory,
            new FixedIdentities(), new FixedTimeProvider());

        var result = await service.CreateAsync(Command() with
        {
            CreatorId = new CreatorId("creator_other_01")
        });

        Assert.Equal(ManualAdventurePlanCreateOutcome.Denied, result.Outcome);
        Assert.Null(authorization.LastRequest);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>An identical retry returns the original result without mutation or another audit.</summary>
    [Fact]
    public async Task CreateAsync_Replay_ReturnsOriginalWithoutMutationOrAudit()
    {
        var transaction = new RecordingTransaction(Creator)
        {
            IdempotencyResult = new(AdventurePlanCreateIdempotencyOutcome.Replay,
                new AdventurePlanId("plan_original_01"), 1)
        };

        var result = await Service(transaction).CreateAsync(Command());

        Assert.Equal(ManualAdventurePlanCreateOutcome.Replayed, result.Outcome);
        Assert.Equal(new AdventurePlanId("plan_original_01"), result.AdventurePlanId);
        Assert.Null(transaction.Repository.Added);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>A reused key with a changed fingerprint fails safely without mutation.</summary>
    [Fact]
    public async Task CreateAsync_ChangedRequestConflict_DoesNotMutate()
    {
        var transaction = new RecordingTransaction(Creator)
        {
            IdempotencyResult = new(AdventurePlanCreateIdempotencyOutcome.Conflict, null, null)
        };

        var result = await Service(transaction).CreateAsync(Command() with { Title = "Changed" });

        Assert.Equal(ManualAdventurePlanCreateOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Added);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>Invalid fields are rejected after authorization and before persistence.</summary>
    [Theory]
    [InlineData("", "Private draft", "2026-11-01", "2026-11-03")]
    [InlineData(" Padded title", "Private draft", "2026-11-01", "2026-11-03")]
    [InlineData("Desert weekend", " Padded description", "2026-11-01", "2026-11-03")]
    [InlineData("Desert weekend", "Private draft", "2026-11-04", "2026-11-03")]
    public async Task CreateAsync_InvalidFields_AreAuthorizedThenRejectedWithoutPersistence(
        string title, string description, string start, string end)
    {
        var authorization = new RecordingAuthorizationEvaluator(Allowed());
        var factory = new RecordingTransactionFactory(new RecordingTransaction(Creator));
        var service = new ManualAdventurePlanCreateService(
            Membership(), authorization, factory, new FixedIdentities(), new FixedTimeProvider());
        var command = Command() with
        {
            Title = title,
            WorkingDescription = description,
            StartDate = DateOnly.Parse(start),
            EndDate = DateOnly.Parse(end)
        };

        var result = await service.CreateAsync(command);

        Assert.Equal(ManualAdventurePlanCreateOutcome.ValidationFailed, result.Outcome);
        Assert.NotNull(authorization.LastRequest);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Persistence failure produces no success result and leaves the transaction uncommitted.</summary>
    [Fact]
    public async Task CreateAsync_PersistenceFailure_RollsBackByDisposal()
    {
        var transaction = new RecordingTransaction(Creator) { ThrowOnAdd = true };

        var result = await Service(transaction).CreateAsync(Command());

        Assert.Equal(ManualAdventurePlanCreateOutcome.Failed, result.Outcome);
        Assert.False(transaction.Committed);
        Assert.True(transaction.Disposed);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>The request fingerprint is stable and excludes generated plan identity.</summary>
    [Fact]
    public async Task CreateAsync_EquivalentRequests_UseDeterministicVersionedFingerprint()
    {
        var first = new RecordingTransaction(Creator);
        var second = new RecordingTransaction(Creator);

        await Service(first).CreateAsync(Command());
        await Service(second, identities: new FixedIdentities("plan_created_02")).CreateAsync(Command());

        Assert.Equal(1, first.IdempotencyReservation!.Fingerprint.Version);
        Assert.Equal(first.IdempotencyReservation.Fingerprint.ToArray(),
            second.IdempotencyReservation!.Fingerprint.ToArray());
    }

    private static ManualAdventurePlanCreateCommand Command() => new(
        Actor, Creator, new PlanningIdempotencyKey("request_1234567890"),
        "Desert weekend", "Private draft", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3));

    private static ManualAdventurePlanCreateService Service(
        RecordingTransaction transaction,
        RecordingAuthorizationEvaluator? authorization = null,
        FixedIdentities? identities = null) => new(
        Membership(), authorization ?? new RecordingAuthorizationEvaluator(Allowed()),
        new RecordingTransactionFactory(transaction), identities ?? new FixedIdentities(),
        new FixedTimeProvider());

    private static AuthorizationDecision Allowed() =>
        AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation);

    private static ICreatorMembershipProvider Membership() => new StubMembershipProvider(new(
        new CreatorMembershipId("membership_planner_01"), User, Creator,
        CreatorMembershipStatus.Active, [CreatorRole.Owner], [], 4, Now));

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(UserId userId, CreatorId creatorId,
            CancellationToken cancellationToken = default) => Task.FromResult(membership);
    }

    private sealed class RecordingAuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public AuthorizationRequest? LastRequest { get; private set; }
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(decision);
        }
    }

    private sealed class FixedIdentities(string planId = "plan_created_01")
        : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => new(planId);
        public DestinationVisitId NewDestinationVisitId() => new("visit_fixed_01");
        public ItineraryDayId NewItineraryDayId() => new("day_fixed_01");
        public PlannedActivityId NewPlannedActivityId() => new("activity_fixed_01");
        public TransportationSegmentId NewTransportationSegmentId() => new("transport_fixed_01");
        public AccommodationId NewAccommodationId() => new("accommodation_fixed_01");
        public AuditEventId NewAuditEventId() => new("audit_created_01");
        public CorrelationId NewCorrelationId() => new("correlation_created_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingTransactionFactory(RecordingTransaction transaction)
        : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public Task<IPlanningTransaction> BeginAsync(CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            Assert.Equal(transaction.CreatorId, creatorId);
            return Task.FromResult<IPlanningTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction(CreatorId creatorId) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public RecordingRepository Repository { get; } = new();
        public RecordingAuditCollector Audits { get; } = new();
        public bool ThrowOnAdd { get => Repository.ThrowOnAdd; set => Repository.ThrowOnAdd = value; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public AdventurePlanCreateReservation? IdempotencyReservation { get; private set; }
        public AdventurePlanCreateIdempotencyResult IdempotencyResult { get; set; } =
            new(AdventurePlanCreateIdempotencyOutcome.Reserved, null, null);
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency =>
            new RecordingIdempotencyStore(this);
        public IRequiredAuditIntentCollector RequiredAuditIntents => Audits;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        private sealed class RecordingIdempotencyStore(RecordingTransaction owner)
            : IAdventurePlanCreateIdempotencyStore
        {
            public Task<AdventurePlanCreateIdempotencyResult> ReserveAsync(CreatorId creatorId,
                AdventurePlanCreateReservation reservation,
                CancellationToken cancellationToken = default)
            {
                Assert.Equal(owner.CreatorId, creatorId);
                owner.IdempotencyReservation = reservation;
                return Task.FromResult(owner.IdempotencyResult);
            }
        }
    }

    private sealed class RecordingAuditCollector : IRequiredAuditIntentCollector
    {
        public List<AuditEventIntent> Items { get; } = [];
        public void AddRequired(AuditEventIntent auditEvent) => Items.Add(auditEvent);
    }

    private sealed class RecordingRepository : IAdventurePlanRepository
    {
        public AdventurePlan? Added { get; private set; }
        public bool ThrowOnAdd { get; set; }
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("database detail");
            Assert.Equal(creatorId, plan.CreatorId);
            Added = plan;
            return Task.CompletedTask;
        }
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan, DestinationVisit destinationVisit, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan, ItineraryDay itineraryDay, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan, PlannedActivity activity, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan, TransportationSegment segment, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId creatorId, AdventurePlan plan, Accommodation accommodation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
