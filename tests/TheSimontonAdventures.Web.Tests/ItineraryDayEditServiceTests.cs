using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects itinerary-day title authorization, replay, concurrency, and audit behavior.</summary>
public sealed class ItineraryDayEditServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ItineraryDayId DayId = new("day_rome_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized edit preserves day context and activities and commits required audit.</summary>
    [Fact]
    public async Task EditAsync_AuthorizedRequest_CommitsOnlyTitleAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditItineraryDayOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        var day = Assert.IsType<ItineraryDay>(transaction.Repository.Day);
        Assert.Equal("Arrival in Rome", day.Title);
        Assert.Equal(DayId, day.Id);
        Assert.Equal(new DateOnly(2027, 1, 3), day.Date);
        Assert.Equal(new DestinationVisitId("visit_rome_01"), day.DestinationVisitId);
        Assert.Equal(new IanaTimeZone("Europe/Rome"), day.TimeZone);
        Assert.Single(transaction.Repository.Plan!.Activities);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>An exact replay is unchanged before stale-version evaluation and performs no mutation.</summary>
    [Fact]
    public async Task EditAsync_ReplayedDesiredState_IsNoOpWithoutAuditOrCommit()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2, title: "Arrival in Rome"));

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditItineraryDayOutcome.Unchanged, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.Null(transaction.Repository.Day);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>A stale divergent desired state cannot overwrite the current title.</summary>
    [Fact]
    public async Task EditAsync_StaleDivergentState_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).EditAsync(Command());
        Assert.Equal(EditItineraryDayOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Day);
    }

    /// <summary>Unknown days and cross-Creator aggregates fail closed.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task EditAsync_UnscopedTarget_IsDenied(bool unknownDay, bool wrongCreator)
    {
        var transaction = new RecordingTransaction(
            Creator, Plan(creator: wrongCreator ? new("creator_other_01") : null));
        var command = unknownDay ? Command() with { ItineraryDayId = new("day_unknown_01") } : Command();
        var result = await Service(transaction).EditAsync(command);
        Assert.Equal(EditItineraryDayOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Archived plans reject itinerary-day edits.</summary>
    [Fact]
    public async Task EditAsync_ArchivedPlan_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(status: PlanningStatus.Archived));
        var result = await Service(transaction).EditAsync(Command());
        Assert.Equal(EditItineraryDayOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Missing membership and authorization denial stop before authoritative loading.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditAsync_Unauthorized_IsDenied(bool missingMembership)
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var service = missingMembership
            ? new ItineraryDayEditService(
                new StubMembershipProvider(null),
                new StubAuthorizationEvaluator(
                    AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
                new RecordingFactory(transaction), new FixedIdentities(), new FixedTimeProvider())
            : Service(transaction, decision: AuthorizationDecision.Deny(
                AuthorizationDenialReason.PermissionRequired));
        var result = await service.EditAsync(Command());
        Assert.Equal(EditItineraryDayOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Invalid titles fail validation without persistence.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" untrimmed")]
    public async Task EditAsync_InvalidTitle_FailsValidation(string title)
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(Command() with { Title = title });
        Assert.Equal(EditItineraryDayOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Day);
    }

    /// <summary>Repository concurrency becomes a safe conflict without commit.</summary>
    [Fact]
    public async Task EditAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).EditAsync(Command());
        Assert.Equal(EditItineraryDayOutcome.Conflict, result.Outcome);
        Assert.False(transaction.Committed);
    }

    private static EditItineraryDayCommand Command() =>
        new(Actor, Creator, PlanId, DayId, 1, "Arrival in Rome");

    private static AdventurePlan Plan(
        long version = 1,
        string title = "Rome day",
        PlanningStatus status = PlanningStatus.Draft,
        CreatorId? creator = null)
    {
        var visitId = new DestinationVisitId("visit_rome_01");
        return new(
            PlanId, creator ?? Creator, "Italy", null,
            status == PlanningStatus.Archived ? AdventureLifecycleStage.Remember : AdventureLifecycleStage.Plan,
            status, new(new(2027, 1, 1), new(2027, 1, 10)), new(version, Created, Created),
            destinationVisits: [new() { Id = visitId, Name = "Rome", Dates = new(new(2027, 1, 2), new(2027, 1, 5)), TimeZone = new("Europe/Rome"), Sequence = 1 }],
            itineraryDays: [new() { Id = DayId, DestinationVisitId = visitId, Date = new(2027, 1, 3), TimeZone = new("Europe/Rome"), Title = title }],
            activities: [new() { Id = new("activity_forum_01"), ItineraryDayId = DayId, Title = "Forum", StartsAtLocal = new(9, 0), Status = PlanItemStatus.Confirmed }]);
    }

    private static CreatorMembershipSnapshot Membership() => new(
        new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
        [CreatorRole.Owner], [], 4, Created);

    private static ItineraryDayEditService Service(
        RecordingTransaction transaction,
        CreatorMembershipSnapshot? membership = null,
        AuthorizationDecision? decision = null) => new(
        new StubMembershipProvider(membership ?? Membership()),
        new StubAuthorizationEvaluator(decision
            ?? AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
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
        public AuditEventId NewAuditEventId() => new("audit_day_edit_01");
        public CorrelationId NewCorrelationId() => new("correlation_day_edit_01");
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
        public ItineraryDay? Day { get; private set; }
        public AdventurePlan? Plan { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => Task.FromResult<AdventurePlan?>(current);
        public Task UpdateItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay d, long v, CancellationToken x = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(p.Id, v);
            Plan = p;
            Day = d;
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
        public Task UpdatePlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation r, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
