using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects reservation authorization, privacy, concurrency, and audit behavior.</summary>
public sealed class ReservationAddServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized request commits a credential-free Proposed reservation and audit.</summary>
    [Fact]
    public async Task AddAsync_AuthorizedRequest_CommitsSafeReservationAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddReservationOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal(PlanItemStatus.Proposed, transaction.Repository.Reservation?.Status);
        Assert.Null(transaction.Repository.Reservation?.ConfirmationReference);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>Blank subjects fail before persistence.</summary>
    [Fact]
    public async Task AddAsync_BlankSubject_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command() with { Subject = " " });
        Assert.Equal(AddReservationOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Reservation);
    }

    /// <summary>Cross-Creator state fails closed.</summary>
    [Fact]
    public async Task AddAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(new("creator_other_01")));
        var result = await Service(transaction).AddAsync(Command());
        Assert.Equal(AddReservationOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Repository concurrency rolls back and becomes a safe conflict.</summary>
    [Fact]
    public async Task AddAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).AddAsync(Command());
        Assert.Equal(AddReservationOutcome.Conflict, result.Outcome);
        Assert.True(transaction.Disposed);
        Assert.False(transaction.Committed);
    }

    private static AddReservationCommand Command() =>
        new(Actor, Creator, PlanId, 1, "Prado Museum tickets");

    private static AdventurePlan Plan(CreatorId? creator = null) => new(
        PlanId, creator ?? Creator, "Spain", null, AdventureLifecycleStage.Plan,
        PlanningStatus.Draft, new(new(2027, 1, 1), new(2027, 1, 10)),
        new(1, Created, Created));

    private static ReservationAddService Service(RecordingTransaction transaction) => new(
        new MembershipProvider(new(new("membership_alpha_01"), User, Creator,
            CreatorMembershipStatus.Active, [CreatorRole.Owner], [], 4, Created)),
        new AuthorizationEvaluator(AuthorizationDecision.Allow(
            AuthorizationAuditRequirement.RequiredMutation)),
        new Factory(transaction), new Identities(), new Clock());

    private sealed class MembershipProvider(CreatorMembershipSnapshot membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CreatorMembershipSnapshot?>(membership);
    }

    private sealed class AuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(decision);
    }

    private sealed class Identities : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => throw new NotSupportedException();
        public DestinationVisitId NewDestinationVisitId() => throw new NotSupportedException();
        public ItineraryDayId NewItineraryDayId() => throw new NotSupportedException();
        public PlannedActivityId NewPlannedActivityId() => throw new NotSupportedException();
        public TransportationSegmentId NewTransportationSegmentId() => throw new NotSupportedException();
        public AccommodationId NewAccommodationId() => throw new NotSupportedException();
        public ReservationId NewReservationId() => new("reservation_prado_01");
        public AuditEventId NewAuditEventId() => new("audit_reservation_01");
        public CorrelationId NewCorrelationId() => new("correlation_reservation_01");
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Created.AddHours(1);
    }

    private sealed class Factory(RecordingTransaction transaction) : IPlanningTransactionFactory
    {
        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IPlanningTransaction>(transaction);
    }

    private sealed class RecordingTransaction(CreatorId creatorId, AdventurePlan current)
        : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public Repository Repository { get; } = new(current);
        public AuditCollector Audits { get; } = new();
        public bool ThrowConcurrency
        {
            get => Repository.ThrowConcurrency;
            set => Repository.ThrowConcurrency = value;
        }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency =>
            throw new NotSupportedException();
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

    private sealed class AuditCollector : IRequiredAuditIntentCollector
    {
        public List<AuditEventIntent> Items { get; } = [];
        public void AddRequired(AuditEventIntent auditEvent) => Items.Add(auditEvent);
    }

    private sealed class Repository(AdventurePlan current) : IAdventurePlanRepository
    {
        public Reservation? Reservation { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult<AdventurePlan?>(current);
        public Task AddReservationAsync(CreatorId creatorId, AdventurePlan plan,
            Reservation reservation, long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency)
            {
                throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            }
            Reservation = reservation;
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
        public Task UpdateItineraryDayAsync(CreatorId c, AdventurePlan p, ItineraryDay d, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdatePlannedActivityAsync(CreatorId c, AdventurePlan p, PlannedActivity a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateTransportationSegmentAsync(CreatorId c, AdventurePlan p, TransportationSegment s, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task AddAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
        public Task UpdateAccommodationAsync(CreatorId c, AdventurePlan p, Accommodation a, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
