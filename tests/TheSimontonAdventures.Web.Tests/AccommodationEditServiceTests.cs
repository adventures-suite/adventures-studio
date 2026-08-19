using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects accommodation edit isolation, replay, concurrency, and audit behavior.</summary>
public sealed class AccommodationEditServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly AccommodationId AccommodationId = new("accommodation_madrid_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized edit persists desired state and required audit intent.</summary>
    [Fact]
    public async Task EditAsync_AuthorizedRequest_CommitsAccommodationAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal("Hotel Central", transaction.Repository.Accommodation?.Name);
        Assert.Equal(PlanItemStatus.Confirmed, transaction.Repository.Accommodation?.Status);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>An exact replay is unchanged even when it carries its original version.</summary>
    [Fact]
    public async Task EditAsync_ExactReplay_ReturnsUnchangedWithoutMutation()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2, desiredState: true));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Unchanged, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.Null(transaction.Repository.Accommodation);
        Assert.False(transaction.Committed);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>A stale request with different desired state cannot overwrite current state.</summary>
    [Fact]
    public async Task EditAsync_StaleDifferentState_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Accommodation);
    }

    /// <summary>An unknown accommodation fails closed without disclosing ownership.</summary>
    [Fact]
    public async Task EditAsync_UnknownAccommodation_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(
            Command() with { AccommodationId = new("accommodation_forged_01") });

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Non-human actors are denied before membership or persistence access.</summary>
    [Fact]
    public async Task EditAsync_NonHumanActor_IsDeniedBeforeLoad()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(Command() with
        {
            Actor = new ActorIdentity(ActorType.System, "system_test", null)
        });

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.Equal(0, transaction.Repository.GetCalls);
    }

    /// <summary>Cross-Creator aggregate state fails closed without mutation.</summary>
    [Fact]
    public async Task EditAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(
            Creator, Plan(creator: new("creator_other_01")));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Missing membership prevents persistence loading.</summary>
    [Fact]
    public async Task EditAsync_MissingMembership_IsDeniedBeforeLoad()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction, includeMembership: false).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.Equal(0, transaction.Repository.GetCalls);
    }

    /// <summary>A denied instance policy prevents authoritative persistence loading.</summary>
    [Fact]
    public async Task EditAsync_DeniedPolicy_IsDeniedBeforeLoad()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction, decision: AuthorizationDecision.Deny(
            AuthorizationDenialReason.PermissionRequired)).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.Equal(0, transaction.Repository.GetCalls);
    }

    /// <summary>Archived plans reject accommodation edits.</summary>
    [Fact]
    public async Task EditAsync_ArchivedPlan_IsDenied()
    {
        var transaction = new RecordingTransaction(
            Creator, Plan(status: PlanningStatus.Archived));
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Reversed inclusive dates fail before protected loading.</summary>
    [Fact]
    public async Task EditAsync_ReversedDates_FailValidationBeforeLoad()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(
            Command() with { EndDate = new(2027, 1, 2) });

        Assert.Equal(EditAccommodationOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, transaction.Repository.GetCalls);
    }

    /// <summary>An invalid IANA time zone fails before protected loading.</summary>
    [Fact]
    public async Task EditAsync_InvalidTimeZone_FailsValidationBeforeLoad()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(
            Command() with { TimeZoneId = "Invalid Zone" });

        Assert.Equal(EditAccommodationOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, transaction.Repository.GetCalls);
    }

    /// <summary>Dates outside the authoritative plan fail after scoped loading.</summary>
    [Fact]
    public async Task EditAsync_DateOutsidePlan_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).EditAsync(
            Command() with { EndDate = new(2027, 2, 1) });

        Assert.Equal(EditAccommodationOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(1, transaction.Repository.GetCalls);
    }

    /// <summary>Repository concurrency becomes a safe conflict without commit.</summary>
    [Fact]
    public async Task EditAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Conflict, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Audit-aware transaction failure becomes a safe failure without commit.</summary>
    [Fact]
    public async Task EditAsync_CommitFailure_ReturnsFailure()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowCommit = true };
        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAccommodationOutcome.Failed, result.Outcome);
        Assert.False(transaction.Committed);
        Assert.Single(transaction.Audits.Items);
    }

    private static EditAccommodationCommand Command() => new(
        Actor, Creator, PlanId, AccommodationId, 1, "Hotel Central",
        new(2027, 1, 3), new(2027, 1, 6), "Europe/Madrid");

    private static AdventurePlan Plan(
        long version = 1,
        bool desiredState = false,
        PlanningStatus status = PlanningStatus.Draft,
        CreatorId? creator = null)
    {
        var accommodation = new Accommodation
        {
            Id = AccommodationId,
            Name = desiredState ? "Hotel Central" : "Original Hotel",
            Dates = desiredState
                ? new(new(2027, 1, 3), new(2027, 1, 6))
                : new(new(2027, 1, 2), new(2027, 1, 5)),
            TimeZone = new("Europe/Madrid"),
            Status = PlanItemStatus.Confirmed
        };
        return new(
            PlanId, creator ?? Creator, "Spain", null,
            status == PlanningStatus.Archived
                ? AdventureLifecycleStage.Remember
                : AdventureLifecycleStage.Plan,
            status, new(new(2027, 1, 1), new(2027, 1, 10)),
            new(version, Created, Created), accommodations: [accommodation]);
    }

    private static AccommodationEditService Service(
        RecordingTransaction transaction,
        bool includeMembership = true,
        AuthorizationDecision? decision = null)
    {
        CreatorMembershipSnapshot? membership = includeMembership ? new(
            new("membership_alpha_01"), User, Creator, CreatorMembershipStatus.Active,
            [CreatorRole.Owner], [], 4, Created) : null;
        return new(
            new StubMembershipProvider(membership),
            new StubAuthorizationEvaluator(decision
                ?? AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation)),
            new RecordingFactory(transaction), new FixedIdentities(), new FixedTimeProvider());
    }

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
        public TransportationSegmentId NewTransportationSegmentId() => throw new NotSupportedException();
        public AccommodationId NewAccommodationId() => throw new NotSupportedException();
        public ReservationId NewReservationId() => throw new NotSupportedException();
        public AuditEventId NewAuditEventId() => new("audit_accommodation_edit_01");
        public CorrelationId NewCorrelationId() => new("correlation_accommodation_edit_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Created.AddHours(1);
    }

    private sealed class RecordingFactory(RecordingTransaction transaction)
        : IPlanningTransactionFactory
    {
        public Task<IPlanningTransaction> BeginAsync(
            CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IPlanningTransaction>(transaction);
    }

    private sealed class RecordingTransaction(CreatorId creatorId, AdventurePlan current)
        : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public RecordingRepository Repository { get; } = new(current);
        public RecordingAuditCollector Audits { get; } = new();
        public bool ThrowConcurrency { get => Repository.ThrowConcurrency; set => Repository.ThrowConcurrency = value; }
        public bool ThrowCommit { get; set; }
        public bool Committed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency =>
            throw new NotSupportedException();
        public IRequiredAuditIntentCollector RequiredAuditIntents => Audits;
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowCommit) throw new InvalidOperationException("Required audit failed.");
            Committed = true;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingAuditCollector : IRequiredAuditIntentCollector
    {
        public List<AuditEventIntent> Items { get; } = [];
        public void AddRequired(AuditEventIntent auditEvent) => Items.Add(auditEvent);
    }

    private sealed class RecordingRepository(AdventurePlan current) : IAdventurePlanRepository
    {
        public Accommodation? Accommodation { get; private set; }
        public int GetCalls { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default)
        {
            GetCalls++;
            return Task.FromResult<AdventurePlan?>(current);
        }
        public Task UpdateAccommodationAsync(CreatorId c, AdventurePlan p,
            Accommodation a, long v, CancellationToken x = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(p.Id, v);
            Accommodation = a;
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
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation r, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
