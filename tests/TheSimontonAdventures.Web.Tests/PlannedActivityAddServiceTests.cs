using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects proposed-activity authorization, day context, concurrency, and audit behavior.</summary>
public sealed class PlannedActivityAddServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ItineraryDayId DayId = new("day_rome_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized request commits a proposed activity and matching audit.</summary>
    [Fact]
    public async Task AddAsync_AuthorizedRequest_CommitsProposedActivityAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddPlannedActivityOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal(PlanItemStatus.Proposed, transaction.Repository.Activity?.Status);
        Assert.Equal(new TimeOnly(10, 0), transaction.Repository.Activity?.StartsAtLocal);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>An activity cannot reference a day outside the authoritative plan.</summary>
    [Fact]
    public async Task AddAsync_UnknownDay_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = new AddPlannedActivityCommand(
            Actor, Creator, PlanId, new("day_forged_01"), 1,
            "Museum", null, null);
        var result = await Service(transaction).AddAsync(command);

        Assert.Equal(AddPlannedActivityOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Activity);
    }

    /// <summary>An end time before its local start time is rejected before persistence.</summary>
    [Fact]
    public async Task AddAsync_ReversedTimes_FailValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = new AddPlannedActivityCommand(
            Actor, Creator, PlanId, DayId, 1, "Museum", new(12, 0), new(10, 0));
        var result = await Service(transaction).AddAsync(command);

        Assert.Equal(AddPlannedActivityOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Activity);
    }

    /// <summary>A stale plan version returns conflict without mutation.</summary>
    [Fact]
    public async Task AddAsync_StaleVersion_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddPlannedActivityOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Activity);
    }

    /// <summary>Cross-Creator state fails closed without mutation.</summary>
    [Fact]
    public async Task AddAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(new("creator_other_01")));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddPlannedActivityOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Repository concurrency rolls back and becomes a safe conflict.</summary>
    [Fact]
    public async Task AddAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddPlannedActivityOutcome.Conflict, result.Outcome);
        Assert.True(transaction.Disposed);
        Assert.False(transaction.Committed);
    }

    private static AddPlannedActivityCommand Command() => new(
        Actor, Creator, PlanId, DayId, 1, "Vatican Museums", new(10, 0), new(12, 0));

    private static AdventurePlan Plan(CreatorId? creator = null, long version = 1)
    {
        var visit = new DestinationVisit
        {
            Id = new("visit_rome_01"),
            Name = "Rome",
            Dates = new(new(2027, 1, 2), new(2027, 1, 5)),
            TimeZone = new("Europe/Rome"),
            Sequence = 1
        };
        var day = new ItineraryDay
        {
            Id = DayId,
            DestinationVisitId = visit.Id,
            Date = new(2027, 1, 3),
            TimeZone = visit.TimeZone,
            Title = "Rome"
        };
        return new(
            PlanId, creator ?? Creator, "Italy", null, AdventureLifecycleStage.Plan,
            PlanningStatus.Draft, new(new(2027, 1, 1), new(2027, 1, 10)),
            new(version, Created, Created), destinationVisits: [visit], itineraryDays: [day]);
    }

    private static PlannedActivityAddService Service(RecordingTransaction transaction) => new(
        new StubMembershipProvider(new(
            new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
            [CreatorRole.Owner], [], 4, Created)),
        new StubAuthorizationEvaluator(
            AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
        new RecordingFactory(transaction), new FixedIdentities(), new FixedTimeProvider());

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
        public AdventurePlanId NewAdventurePlanId() => throw new InvalidOperationException();
        public DestinationVisitId NewDestinationVisitId() => throw new InvalidOperationException();
        public ItineraryDayId NewItineraryDayId() => throw new InvalidOperationException();
        public PlannedActivityId NewPlannedActivityId() => new("activity_vatican_01");
        public TransportationSegmentId NewTransportationSegmentId() => new("transport_fixed_01");
        public AccommodationId NewAccommodationId() => new("accommodation_fixed_01");
        public ReservationId NewReservationId() => new("reservation_fixed_01");
        public AuditEventId NewAuditEventId() => new("audit_activity_01");
        public CorrelationId NewCorrelationId() => new("correlation_activity_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Created.AddHours(1);
    }

    private sealed class RecordingFactory(RecordingTransaction transaction) : IPlanningTransactionFactory
    {
        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IPlanningTransaction>(transaction);
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
        public PlannedActivity? Activity { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult(current);
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan,
            PlannedActivity activity, long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            Activity = activity;
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
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan, TransportationSegment segment, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId creatorId, AdventurePlan plan, Accommodation accommodation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId creatorId, AdventurePlan plan, Reservation reservation, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
