using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects destination-visit authorization, validation, concurrency, and audit behavior.</summary>
public sealed class DestinationVisitAddServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized request appends at the next sequence and commits matching audit intent.</summary>
    [Fact]
    public async Task AddAsync_AuthorizedRequest_CommitsVisitAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal("Rome", transaction.Repository.Visit?.Name);
        Assert.Equal(1, transaction.Repository.Visit?.Sequence);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(Permissions.AdventurePlanEdit, audit.Permission);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>A stale request returns a safe conflict without persistence or audit.</summary>
    [Fact]
    public async Task AddAsync_StaleVersion_DoesNotMutate()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Visit);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>Cross-Creator authoritative state fails closed without mutation.</summary>
    [Fact]
    public async Task AddAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(new("creator_other_01")));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Denied, result.Outcome);
        Assert.Null(transaction.Repository.Visit);
        Assert.False(transaction.Committed);
    }

    /// <summary>Archived plans reject destination mutation.</summary>
    [Fact]
    public async Task AddAsync_ArchivedPlan_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(status: PlanningStatus.Archived));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Unknown IANA identifiers fail validation before Planning persistence.</summary>
    [Fact]
    public async Task AddAsync_UnknownTimeZone_FailsBeforeTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var command = new AddDestinationVisitCommand(
            Actor, Creator, PlanId, 1, "Rome", new(2027, 1, 2), new(2027, 1, 3), "Mars/Olympus");
        var result = await Service(factory).AddAsync(command);

        Assert.Equal(AddDestinationVisitOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Visit dates outside the plan fail validation without mutation.</summary>
    [Fact]
    public async Task AddAsync_DatesOutsidePlan_FailValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = new AddDestinationVisitCommand(
            Actor, Creator, PlanId, 1, "Rome", new(2027, 1, 9), new(2027, 1, 11), "Europe/Rome");
        var result = await Service(transaction).AddAsync(command);

        Assert.Equal(AddDestinationVisitOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Visit);
    }

    /// <summary>A denied mutation policy prevents transaction access.</summary>
    [Fact]
    public async Task AddAsync_DeniedPolicy_DoesNotOpenTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var service = Service(factory, AuthorizationDecision.Deny(
            AuthorizationDenialReason.PermissionRequired));
        var result = await service.AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Denied, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Repository concurrency is translated to a safe conflict and rolls back.</summary>
    [Fact]
    public async Task AddAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddDestinationVisitOutcome.Conflict, result.Outcome);
        Assert.False(transaction.Committed);
        Assert.True(transaction.Disposed);
    }

    private static AddDestinationVisitCommand Command() => new(
        Actor, Creator, PlanId, 1, "Rome", new(2027, 1, 2), new(2027, 1, 3), "Europe/Rome");

    private static AdventurePlan Plan(
        CreatorId? creator = null,
        long version = 1,
        PlanningStatus status = PlanningStatus.Draft) => new(
        PlanId, creator ?? Creator, "Italy", null,
        status == PlanningStatus.Archived ? AdventureLifecycleStage.Remember : AdventureLifecycleStage.Plan,
        status,
        new(new(2027, 1, 1), new(2027, 1, 10)), new(version, Created, Created));

    private static DestinationVisitAddService Service(
        RecordingTransaction transaction,
        AuthorizationDecision? decision = null) => Service(new RecordingFactory(transaction), decision);

    private static DestinationVisitAddService Service(
        RecordingFactory factory,
        AuthorizationDecision? decision = null) => new(
        new StubMembershipProvider(new(
            new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
            [CreatorRole.Owner], [], 4, Created)),
        new StubAuthorizationEvaluator(decision
            ?? AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
        factory,
        new FixedIdentities(),
        new FixedTimeProvider());

    private sealed class StubAuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(decision);
    }

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId,
            CreatorId creatorId,
            CancellationToken cancellationToken = default) => Task.FromResult(membership);
    }

    private sealed class FixedIdentities : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => throw new InvalidOperationException();
        public DestinationVisitId NewDestinationVisitId() => new("visit_rome_01");
        public ItineraryDayId NewItineraryDayId() => new("day_fixed_01");
        public PlannedActivityId NewPlannedActivityId() => new("activity_fixed_01");
        public TransportationSegmentId NewTransportationSegmentId() => new("transport_fixed_01");
        public AuditEventId NewAuditEventId() => new("audit_visit_01");
        public CorrelationId NewCorrelationId() => new("correlation_visit_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingFactory(RecordingTransaction transaction)
        : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            Assert.Equal(transaction.CreatorId, creatorId);
            return Task.FromResult<IPlanningTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction(CreatorId creatorId, AdventurePlan? current)
        : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public RecordingRepository Repository { get; } = new(current);
        public RecordingAuditCollector Audits { get; } = new();
        public bool ThrowConcurrency { get => Repository.ThrowConcurrency; set => Repository.ThrowConcurrency = value; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency => throw new NotSupportedException();
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
    }

    private sealed class RecordingAuditCollector : IRequiredAuditIntentCollector
    {
        public List<AuditEventIntent> Items { get; } = [];
        public void AddRequired(AuditEventIntent auditEvent) => Items.Add(auditEvent);
    }

    private sealed class RecordingRepository(AdventurePlan? current) : IAdventurePlanRepository
    {
        public DestinationVisit? Visit { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult(current);
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan,
            DestinationVisit destinationVisit, long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            Visit = destinationVisit;
            return Task.CompletedTask;
        }
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan, ItineraryDay itineraryDay, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan, PlannedActivity activity, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan, TransportationSegment segment, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
