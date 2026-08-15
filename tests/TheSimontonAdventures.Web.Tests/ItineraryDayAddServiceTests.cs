using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Protects itinerary-day authorization, visit context, concurrency, and audit behavior.</summary>
public sealed class ItineraryDayAddServiceTests
{
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly UserId User = new("user_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_alpha_01");
    private static readonly DestinationVisitId VisitId = new("visit_rome_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An authorized request derives visit time zone and commits matching audit.</summary>
    [Fact]
    public async Task AddAsync_AuthorizedRequest_CommitsVisitScopedDayAndAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddItineraryDayOutcome.Added, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        Assert.Equal("Europe/Rome", transaction.Repository.Day?.TimeZone.Value);
        Assert.Equal(VisitId, transaction.Repository.Day?.DestinationVisitId);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
    }

    /// <summary>A stale plan version returns conflict before mutation.</summary>
    [Fact]
    public async Task AddAsync_StaleVersion_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddItineraryDayOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Day);
        Assert.False(transaction.Committed);
    }

    /// <summary>A visit outside the authoritative plan cannot receive a day.</summary>
    [Fact]
    public async Task AddAsync_UnknownVisit_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = new AddItineraryDayCommand(
            Actor, Creator, PlanId, new("visit_forged_01"), 1,
            new(2027, 1, 3), "Rome arrival");
        var result = await Service(transaction).AddAsync(command);

        Assert.Equal(AddItineraryDayOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Day);
    }

    /// <summary>A day outside its destination visit is rejected.</summary>
    [Fact]
    public async Task AddAsync_DateOutsideVisit_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());
        var command = new AddItineraryDayCommand(
            Actor, Creator, PlanId, VisitId, 1, new(2027, 1, 8), "Late day");
        var result = await Service(transaction).AddAsync(command);

        Assert.Equal(AddItineraryDayOutcome.ValidationFailed, result.Outcome);
    }

    /// <summary>A duplicate local date is rejected before persistence.</summary>
    [Fact]
    public async Task AddAsync_DuplicateDate_FailsValidation()
    {
        var existing = new ItineraryDay
        {
            Id = new("day_existing_01"), DestinationVisitId = VisitId,
            Date = new(2027, 1, 3), TimeZone = new("Europe/Rome"), Title = "Existing"
        };
        var transaction = new RecordingTransaction(Creator, Plan(days: [existing]));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddItineraryDayOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Day);
    }

    /// <summary>Cross-Creator state fails closed without mutation.</summary>
    [Fact]
    public async Task AddAsync_MismatchedOwnership_IsDenied()
    {
        var transaction = new RecordingTransaction(Creator, Plan(new("creator_other_01")));
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddItineraryDayOutcome.Denied, result.Outcome);
        Assert.False(transaction.Committed);
    }

    /// <summary>Repository concurrency rolls back and becomes a safe conflict.</summary>
    [Fact]
    public async Task AddAsync_PersistenceConcurrency_ReturnsConflict()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };
        var result = await Service(transaction).AddAsync(Command());

        Assert.Equal(AddItineraryDayOutcome.Conflict, result.Outcome);
        Assert.True(transaction.Disposed);
        Assert.False(transaction.Committed);
    }

    private static AddItineraryDayCommand Command() => new(
        Actor, Creator, PlanId, VisitId, 1, new(2027, 1, 3), "Rome arrival");

    private static AdventurePlan Plan(
        CreatorId? creator = null,
        long version = 1,
        IReadOnlyList<ItineraryDay>? days = null)
    {
        var visit = new DestinationVisit
        {
            Id = VisitId, Name = "Rome", Dates = new(new(2027, 1, 2), new(2027, 1, 5)),
            TimeZone = new("Europe/Rome"), Sequence = 1
        };
        return new(
            PlanId, creator ?? Creator, "Italy", null, AdventureLifecycleStage.Plan,
            PlanningStatus.Draft, new(new(2027, 1, 1), new(2027, 1, 10)),
            new(version, Created, Created), destinationVisits: [visit], itineraryDays: days);
    }

    private static ItineraryDayAddService Service(RecordingTransaction transaction) => new(
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
        public ItineraryDayId NewItineraryDayId() => new("day_rome_01");
        public AuditEventId NewAuditEventId() => new("audit_day_01");
        public CorrelationId NewCorrelationId() => new("correlation_day_01");
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
        public ItineraryDay? Day { get; private set; }
        public bool ThrowConcurrency { get; set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult(current);
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan,
            ItineraryDay itineraryDay, long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency) throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            Day = itineraryDay;
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
    }
}
