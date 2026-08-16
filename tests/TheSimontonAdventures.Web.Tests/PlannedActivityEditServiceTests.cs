using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects planned-activity edit authorization, idempotency, concurrency, and audit behavior.</summary>
public sealed class PlannedActivityEditServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly PlannedActivityId ActivityId = new("activity_museum_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized edit atomically persists desired state and required audit intent.</summary>
    [Fact]
    public async Task EditAsync_AuthorizedRequest_CommitsActivityAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal("Vatican Museums", transaction.Repository.Activity?.Title);
        Assert.Equal(PlanItemStatus.Proposed, transaction.Repository.Activity?.Status);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>A replay of authoritative desired state is a no-op even with its original version.</summary>
    [Fact]
    public async Task EditAsync_ReplayedDesiredState_ReturnsUnchangedWithoutAudit()
    {
        var transaction = new RecordingTransaction(
            Creator, Plan(version: 2, title: "Vatican Museums", startsAt: new(10, 0), endsAt: new(12, 0)));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Unchanged, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.False(transaction.Committed);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>A stale request with different desired state cannot overwrite current state.</summary>
    [Fact]
    public async Task EditAsync_StaleDifferentState_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Activity);
    }

    /// <summary>An unknown activity fails closed without revealing whether another plan owns it.</summary>
    [Fact]
    public async Task EditAsync_UnknownActivity_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(Command(new("activity_forged_01")));

        Assert.Equal(EditPlannedActivityOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Cross-Creator aggregate state fails closed without mutation.</summary>
    [Fact]
    public async Task EditAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(
            Creator, Plan(creator: new("creator_other_01")));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>An end time before its local start is rejected before persistence.</summary>
    [Fact]
    public async Task EditAsync_ReversedTimes_FailValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = Command() with { StartsAtLocal = new(14, 0), EndsAtLocal = new(12, 0) };
        var result = await Service(transaction).EditAsync(command);

        Assert.Equal(EditPlannedActivityOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Activity);
    }

    /// <summary>Archived plans reject activity edits.</summary>
    [Fact]
    public async Task EditAsync_ArchivedPlan_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(status: PlanningStatus.Archived));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Repository concurrency rolls back and becomes a safe conflict.</summary>
    [Fact]
    public async Task EditAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditPlannedActivityOutcome.Conflict, result.Outcome);
        Assert.False(transaction.Committed);
    }

    private static EditPlannedActivityCommand Command(PlannedActivityId? activityId = null) => new(
        Actor, Creator, PlanId, activityId ?? ActivityId, 1,
        "Vatican Museums", new(10, 0), new(12, 0));

    private static AdventurePlan Plan(
        long version = 1,
        string title = "Museum",
        PlanningStatus status = PlanningStatus.Draft,
        TimeOnly? startsAt = null,
        TimeOnly? endsAt = null,
        CreatorId? creator = null)
    {
        var dayId = new ItineraryDayId("day_rome_01");
        return new(
            PlanId, creator ?? Creator, "Italy", null,
            status == PlanningStatus.Archived ? AdventureLifecycleStage.Remember : AdventureLifecycleStage.Plan,
            status,
            new(new(2027, 1, 1), new(2027, 1, 10)), new(version, Created, Created),
            itineraryDays: [new() { Id = dayId, Date = new(2027, 1, 3), TimeZone = new("Europe/Rome"), Title = "Rome" }],
            activities: [new() { Id = ActivityId, ItineraryDayId = dayId, Title = title, StartsAtLocal = startsAt ?? new(9, 0), EndsAtLocal = endsAt ?? new(11, 0), Status = PlanItemStatus.Proposed }]);
    }

    private static PlannedActivityEditService Service(RecordingTransaction transaction) => new(
        new StubMembershipProvider(new(
            new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
            [CreatorRole.Owner], [], 4, Created)),
        new StubAuthorizationEvaluator(
            AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
        new RecordingFactory(transaction), new FixedIdentities(), new FixedTimeProvider());

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership) : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
    }

    private sealed class StubAuthorizationEvaluator(AuthorizationDecision decision) : IAuthorizationPolicyEvaluator
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
        public TransportationSegmentId NewTransportationSegmentId() => throw new NotSupportedException();
        public AccommodationId NewAccommodationId() => throw new NotSupportedException();
        public ReservationId NewReservationId() => throw new NotSupportedException();
        public AuditEventId NewAuditEventId() => new("audit_activity_edit_01");
        public CorrelationId NewCorrelationId() => new("correlation_activity_edit_01");
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

    private sealed class RecordingTransaction(CreatorId creatorId, AdventurePlan current) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public RecordingRepository Repository { get; } = new(current);
        public RecordingAuditCollector Audits { get; } = new();
        public bool ThrowConcurrency { get => Repository.ThrowConcurrency; set => Repository.ThrowConcurrency = value; }
        public bool Committed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency => throw new NotSupportedException();
        public IRequiredAuditIntentCollector RequiredAuditIntents => Audits;
        public Task CommitAsync(CancellationToken cancellationToken = default) { Committed = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingAuditCollector : IRequiredAuditIntentCollector
    {
        public List<AuditEventIntent> Items { get; } = [];
        public void AddRequired(AuditEventIntent auditEvent) => Items.Add(auditEvent);
    }

    private sealed class RecordingRepository(AdventurePlan current) : IAdventurePlanRepository
    {
        public PlannedActivity? Activity { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => Task.FromResult<AdventurePlan?>(current);
        public Task UpdatePlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(p.Id, v);
            Activity = a;
            return Task.CompletedTask;
        }
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAsync(CreatorId c, AdventurePlan p, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateOverviewAsync(CreatorId c, AdventurePlan p, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddDestinationVisitAsync(CreatorId c, AdventurePlan p, DestinationVisit d, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay d, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation r, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
