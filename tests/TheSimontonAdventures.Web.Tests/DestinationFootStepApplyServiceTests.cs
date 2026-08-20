using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects Destination FootStep authorization, replay, provenance, and audit behavior.</summary>
public sealed class DestinationFootStepApplyServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 20, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized application commits destination, provenance, and audit together.</summary>
    [Fact]
    public async Task ApplyAsync_AuthorizedRequest_CommitsAtomicEvidence()
    {
        var transaction = new RecordingTransaction(Plan());

        var result = await Service(transaction).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal("Lisbon, Portugal", transaction.Repository.Visit?.Name);
        Assert.Equal("Europe/Lisbon", transaction.Repository.Visit?.TimeZone.Value);
        Assert.Equal(["resolve", "destination", "provenance", "audit", "commit"], transaction.Events);
        var evidence = Assert.Single(transaction.Applications.Added);
        Assert.Equal("footstep_destination_lisbon_gateway", evidence.FootStepId);
        Assert.Equal("1.0", evidence.FootStepVersion);
        Assert.Equal("DestinationVisit", evidence.TargetType);
        Assert.Equal("visit_lisbon_01", evidence.TargetId);
        Assert.Single(transaction.Audits.Items);
    }

    /// <summary>An identical retry returns the durable result without another mutation or commit.</summary>
    [Fact]
    public async Task ApplyAsync_CommittedReplay_DoesNotMutate()
    {
        var transaction = new RecordingTransaction(Plan(version: 2));
        transaction.Applications.Next = new(PlannerFootStepApplicationOutcome.Replay, "visit_original", 2);

        var result = await Service(transaction).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Replayed, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.Null(transaction.Repository.Visit);
        Assert.Empty(transaction.Applications.Added);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>A reused key with another fingerprint fails closed without mutation.</summary>
    [Fact]
    public async Task ApplyAsync_KeyConflict_DoesNotMutate()
    {
        var transaction = new RecordingTransaction(Plan());
        transaction.Applications.Next = new(PlannerFootStepApplicationOutcome.Conflict, null, null);

        var result = await Service(transaction).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Visit);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>A stale plan version rolls back the serialized reservation before mutation.</summary>
    [Fact]
    public async Task ApplyAsync_StaleVersion_DoesNotPersistEvidence()
    {
        var transaction = new RecordingTransaction(Plan(version: 2));

        var result = await Service(transaction).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Conflict, result.Outcome);
        Assert.Equal(["resolve"], transaction.Events);
        Assert.Empty(transaction.Applications.Added);
        Assert.False(transaction.Committed);
    }

    /// <summary>An unavailable exact source version is denied before Planning persistence.</summary>
    [Fact]
    public async Task ApplyAsync_MissingExactSource_DoesNotOpenTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Plan()));
        var result = await Service(factory, resolver: new MissingResolver()).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Denied, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Archived plans reject FootStep application without provenance.</summary>
    [Fact]
    public async Task ApplyAsync_ArchivedPlan_IsDenied()
    {
        var transaction = new RecordingTransaction(Plan(status: PlanningStatus.Archived));

        var result = await Service(transaction).ApplyAsync(Command());

        Assert.Equal(ApplyDestinationFootStepOutcome.Denied, result.Outcome);
        Assert.Empty(transaction.Applications.Added);
        Assert.False(transaction.Committed);
    }

    private static ApplyDestinationFootStepCommand Command() => new(
        Actor, Creator, PlanId, 1, new("footstep-apply-key-0001"),
        "footstep_destination_lisbon_gateway", "1.0",
        new(2027, 5, 2), new(2027, 5, 4), "Europe/Lisbon");

    private static AdventurePlan Plan(long version = 1, PlanningStatus status = PlanningStatus.Draft) => new(
        PlanId, Creator, "Portugal", null,
        status == PlanningStatus.Archived ? AdventureLifecycleStage.Remember : AdventureLifecycleStage.Plan,
        status, new(new(2027, 5, 1), new(2027, 5, 10)), new(version, Now, Now));

    private static DestinationFootStepApplyService Service(
        RecordingTransaction transaction,
        IPlannerFootStepUseResolver? resolver = null) =>
        Service(new RecordingFactory(transaction), resolver);

    private static DestinationFootStepApplyService Service(
        RecordingFactory factory,
        IPlannerFootStepUseResolver? resolver = null) => new(
        new MembershipProvider(), new AuthorizationEvaluator(), resolver ?? new UseResolver(),
        factory, new FixedIdentities(), new FixedTimeProvider());

    private sealed class MembershipProvider : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CreatorMembershipSnapshot?>(new(
                new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
                [CreatorRole.Owner], [], 4, Now));
    }

    private sealed class AuthorizationEvaluator : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation));
    }

    private sealed class UseResolver : IPlannerFootStepUseResolver
    {
        public Task<AuthorizedPlannerFootStepUse?> ResolveAsync(
            ActorIdentity actor, CreatorId creatorId, string footStepId, string footStepVersion,
            CancellationToken cancellationToken = default) => Task.FromResult<AuthorizedPlannerFootStepUse?>(new(
                new PlannerFootStepDefinition
                {
                    Id = footStepId,
                    Version = footStepVersion,
                    Kind = "destination",
                    Title = "Lisbon cultural gateway",
                    Summary = "Fictional reviewed destination draft.",
                    Attribution = "AdventuresSuite fictional editorial demo",
                    Freshness = "Demo snapshot",
                    ContextKinds = new HashSet<PlannerFootStepContextKind> { PlannerFootStepContextKind.Adventure },
                    DestinationDraft = new("Lisbon, Portugal", "Europe/Lisbon")
                }, "development:footstep_destination_lisbon_gateway:1.0"));
    }

    private sealed class MissingResolver : IPlannerFootStepUseResolver
    {
        public Task<AuthorizedPlannerFootStepUse?> ResolveAsync(
            ActorIdentity actor, CreatorId creatorId, string footStepId, string footStepVersion,
            CancellationToken cancellationToken = default) => Task.FromResult<AuthorizedPlannerFootStepUse?>(null);
    }

    private sealed class FixedIdentities : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => throw new NotSupportedException();
        public DestinationVisitId NewDestinationVisitId() => new("visit_lisbon_01");
        public ItineraryDayId NewItineraryDayId() => throw new NotSupportedException();
        public PlannedActivityId NewPlannedActivityId() => throw new NotSupportedException();
        public TransportationSegmentId NewTransportationSegmentId() => throw new NotSupportedException();
        public AccommodationId NewAccommodationId() => throw new NotSupportedException();
        public ReservationId NewReservationId() => throw new NotSupportedException();
        public AuditEventId NewAuditEventId() => new("audit_footstep_01");
        public CorrelationId NewCorrelationId() => new("correlation_footstep_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
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

    private sealed class RecordingTransaction(AdventurePlan current) : IPlanningTransaction
    {
        public CreatorId CreatorId => Creator;
        public List<string> Events { get; } = [];
        public RecordingRepository Repository { get; } = new(current);
        public RecordingApplicationStore Applications { get; } = new();
        public RecordingAudits Audits { get; } = new();
        public bool Committed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository.Attach(Events);
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency => throw new NotSupportedException();
        public IPlannerFootStepApplicationStore PlannerFootStepApplications => Applications.Attach(Events);
        public IRequiredAuditIntentCollector RequiredAuditIntents => Audits.Attach(Events);
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Events.Add("commit");
            Committed = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingApplicationStore : IPlannerFootStepApplicationStore
    {
        private List<string> events = [];
        public PlannerFootStepApplicationResult Next { get; set; } =
            new(PlannerFootStepApplicationOutcome.Reserved, "visit_lisbon_01", 2);
        public List<PlannerFootStepApplicationReservation> Added { get; } = [];
        public RecordingApplicationStore Attach(List<string> target) { events = target; return this; }
        public Task<PlannerFootStepApplicationResult> ResolveAsync(
            CreatorId creatorId, PlannerFootStepApplicationReservation reservation,
            CancellationToken cancellationToken = default)
        {
            events.Add("resolve");
            return Task.FromResult(Next);
        }
        public Task AddAsync(CreatorId creatorId, PlannerFootStepApplicationReservation reservation,
            CancellationToken cancellationToken = default)
        {
            events.Add("provenance");
            Added.Add(reservation);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAudits : IRequiredAuditIntentCollector
    {
        private List<string> events = [];
        public List<AuditEventIntent> Items { get; } = [];
        public RecordingAudits Attach(List<string> target) { events = target; return this; }
        public void AddRequired(AuditEventIntent auditEvent) { events.Add("audit"); Items.Add(auditEvent); }
    }

    private sealed class RecordingRepository(AdventurePlan current) : IAdventurePlanRepository
    {
        private List<string> events = [];
        public DestinationVisit? Visit { get; private set; }
        public RecordingRepository Attach(List<string> target) { events = target; return this; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult<AdventurePlan?>(current);
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan,
            DestinationVisit destinationVisit, long expectedVersion, CancellationToken cancellationToken = default)
        {
            events.Add("destination"); Visit = destinationVisit; return Task.CompletedTask;
        }
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAsync(CreatorId c, AdventurePlan p, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay d, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay d, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdatePlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation r, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
