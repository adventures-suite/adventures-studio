using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects transportation authorization, validation, concurrency, and audit behavior.</summary>
public sealed class TransportationSegmentAddServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized segment commits as Proposed with matching audit versions.</summary>
    [Fact]
    public async Task AddAsync_AuthorizedSegment_CommitsProposedTransportationAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddTransportationSegmentOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal(PlanItemStatus.Proposed, transaction.Repository.Segment?.Status);
        Assert.Equal("America/Phoenix", transaction.Repository.Segment?.DepartureTimeZone.Value);
        Assert.Equal("Europe/Rome", transaction.Repository.Segment?.ArrivalTimeZone.Value);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>Unknown IANA zones fail before Planning persistence.</summary>
    [Fact]
    public async Task AddAsync_UnknownTimeZone_FailsBeforeTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var result = await Service(factory).AddAsync(Command() with
        {
            ArrivalTimeZoneId = "Mars/Olympus"
        });

        Assert.Equal(AddTransportationSegmentOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Transportation dates outside the plan fail without mutation.</summary>
    [Fact]
    public async Task AddAsync_DateOutsidePlan_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command() with
        {
            ArrivalDate = new(2027, 1, 11)
        });

        Assert.Equal(AddTransportationSegmentOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Segment);
    }

    /// <summary>Cross-Creator authoritative state fails closed without mutation.</summary>
    [Fact]
    public async Task AddAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(new("creator_other_01")));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddTransportationSegmentOutcome.Denied, result.Outcome);
        Assert.Null(transaction.Repository.Segment);
        Assert.False(transaction.Committed);
    }

    /// <summary>A stale rendered version returns conflict before mutation.</summary>
    [Fact]
    public async Task AddAsync_StaleVersion_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddTransportationSegmentOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Segment);
    }

    /// <summary>Repository concurrency rolls back and becomes a safe conflict.</summary>
    [Fact]
    public async Task AddAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddTransportationSegmentOutcome.Conflict, result.Outcome);
        Assert.True(transaction.Disposed);
        Assert.False(transaction.Committed);
    }

    private static AddTransportationSegmentCommand Command() => new(
        Actor, Creator, PlanId, 1, "Flight", "Phoenix", "Rome",
        new(2027, 1, 2), new(10, 0), "America/Phoenix",
        new(2027, 1, 3), new(9, 0), "Europe/Rome");

    private static AdventurePlan Plan(CreatorId? creator = null, long version = 1) => new(
        PlanId, creator ?? Creator, "Italy", null, AdventureLifecycleStage.Plan,
        PlanningStatus.Draft, new(new(2027, 1, 1), new(2027, 1, 10)),
        new(version, Created, Created));

    private static TransportationSegmentAddService Service(RecordingTransaction transaction) =>
        Service(new RecordingFactory(transaction));

    private static TransportationSegmentAddService Service(RecordingFactory factory) => new(
        new StubMembershipProvider(new(
            new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
            [CreatorRole.Owner], [], 4, Created)),
        new StubAuthorizationEvaluator(
            AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
        factory, new FixedIdentities(), new FixedTimeProvider());

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

    private sealed class FixedIdentities : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => throw new NotSupportedException();
        public DestinationVisitId NewDestinationVisitId() => throw new NotSupportedException();
        public ItineraryDayId NewItineraryDayId() => throw new NotSupportedException();
        public PlannedActivityId NewPlannedActivityId() => throw new NotSupportedException();
        public TransportationSegmentId NewTransportationSegmentId() => new("transport_flight_01");
        public AuditEventId NewAuditEventId() => new("audit_transport_01");
        public CorrelationId NewCorrelationId() => new("correlation_transport_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Created.AddHours(1);
    }

    private sealed class RecordingFactory(RecordingTransaction transaction) : IPlanningTransactionFactory
    {
        public int BeginCount { get; private set; }
        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default)
        {
            BeginCount++;
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
        public TransportationSegment? Segment { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult(current);
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan,
            TransportationSegment segment, long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            Segment = segment;
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
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan, DestinationVisit destinationVisit, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan, ItineraryDay itineraryDay, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan, PlannedActivity activity, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
