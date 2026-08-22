using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies independent, authorized, atomic template materialization.</summary>
public sealed class AdventureTemplateInstantiateServiceTests
{
    private static readonly UserId User = new("user_template_01");
    private static readonly CreatorId Creator = new("creator_template_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly AdventureTemplateVersionId TemplateVersion = new("portugal-rail", "1.0");

    /// <summary>A valid use creates one complete independent plan and immutable origin atomically.</summary>
    [Fact]
    public async Task InstantiateAsync_ValidTemplate_CommitsAggregateOriginAndAudit()
    {
        var transaction = new RecordingTransaction(Creator);
        var service = Service(transaction);

        var result = await service.InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.Created, result.Outcome);
        Assert.True(transaction.Committed);
        var plan = Assert.IsType<AdventurePlan>(transaction.Repository.Added);
        Assert.Equal(new PlanningDateRange(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 6)), plan.Dates);
        Assert.Equal(2, plan.DestinationVisits.Count);
        Assert.Equal(2, plan.ItineraryDays.Count);
        Assert.Single(plan.Activities);
        Assert.Single(plan.Transportation);
        Assert.Single(plan.Accommodations);
        Assert.All(plan.DestinationVisits, visit => Assert.StartsWith("visit_new_", visit.Id.Value));
        Assert.All(plan.ItineraryDays, day => Assert.StartsWith("day_new_", day.Id.Value));
        var origin = Assert.IsType<AdventurePlanTemplateOrigin>(transaction.Origins.Added);
        Assert.Equal(TemplateVersion, origin.TemplateVersion);
        Assert.Equal(AdventureTemplateOwnerType.Platform, origin.TemplateOwnerType);
        Assert.Equal("en-US", origin.SourceLocale);
        Assert.Equal(PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1,
            transaction.Reservation!.Operation);
        Assert.Single(transaction.Audits);
    }

    /// <summary>An unauthorized source use cannot create or disclose a plan.</summary>
    [Fact]
    public async Task InstantiateAsync_SourceUseDenied_DoesNotBeginTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator));
        var service = new AdventureTemplateInstantiateService(
            Membership(), new AllowEvaluator(), new StubUseResolver(null), factory,
            new FixedIdentities(), new FixedTimeProvider());

        var result = await service.InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.Denied, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>An exact retry returns the original plan without materialization or origin insertion.</summary>
    [Fact]
    public async Task InstantiateAsync_Replay_ReturnsOriginalWithoutMutation()
    {
        var transaction = new RecordingTransaction(Creator)
        {
            IdempotencyResult = new(
                AdventurePlanCreateIdempotencyOutcome.Replay,
                new AdventurePlanId("plan_original_01"), 1)
        };

        var result = await Service(transaction).InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.Replayed, result.Outcome);
        Assert.Equal(new AdventurePlanId("plan_original_01"), result.AdventurePlanId);
        Assert.Null(transaction.Repository.Added);
        Assert.Null(transaction.Origins.Added);
        Assert.False(transaction.Committed);
    }

    /// <summary>An origin-store failure leaves the transaction uncommitted for rollback on disposal.</summary>
    [Fact]
    public async Task InstantiateAsync_OriginFailure_DoesNotCommitPartialPlan()
    {
        var transaction = new RecordingTransaction(Creator) { ThrowOnOrigin = true };

        var result = await Service(transaction).InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.Failed, result.Outcome);
        Assert.False(transaction.Committed);
        Assert.True(transaction.Disposed);
    }

    /// <summary>A reviewed origin replaces only typed origin slots and is covered by plan creation.</summary>
    [Fact]
    public async Task InstantiateAsync_OriginAwareTemplate_MaterializesReviewedStartingPlace()
    {
        var transaction = new RecordingTransaction(Creator);
        var command = Command() with
        {
            ConfiguredOrigin = new("Phoenix, Arizona", new IanaTimeZone("America/Phoenix")),
            TravelEstimate = new(1300, 450),
            TravelStops =
            [
                new(AdventureTemplateTravelDirection.Outbound, 1, "Albuquerque, New Mexico"),
                new(AdventureTemplateTravelDirection.Outbound, 2, "Denver, Colorado"),
                new(AdventureTemplateTravelDirection.Return, 1, "Cheyenne, Wyoming"),
                new(AdventureTemplateTravelDirection.Return, 2, "Moab, Utah")
            ]
        };

        var result = await Service(transaction, new(OriginBlueprint(), "decision_origin_0001"))
            .InstantiateAsync(command);

        Assert.Equal(AdventureTemplateInstantiateOutcome.Created, result.Outcome);
        var plan = Assert.IsType<AdventurePlan>(transaction.Repository.Added);
        Assert.Equal(2, plan.DestinationVisits.Count(item => item.Name == "Phoenix, Arizona"));
        Assert.Equal(7, plan.DestinationVisits.Count);
        Assert.All(
            plan.DestinationVisits.Where(item => item.Name == "Phoenix, Arizona"),
            item => Assert.Equal(new IanaTimeZone("America/Phoenix"), item.TimeZone));
        Assert.Equal(6, plan.Transportation.Count);
        Assert.Equal("Phoenix, Arizona", plan.Transportation[0].From);
        Assert.Equal("Albuquerque, New Mexico", plan.Transportation[0].To);
        Assert.Equal(new IanaTimeZone("America/Phoenix"), plan.Transportation[0].DepartureTimeZone);
        Assert.Equal("Phoenix, Arizona", plan.Transportation[^1].To);
        Assert.Equal(new IanaTimeZone("America/Phoenix"), plan.Transportation[^1].ArrivalTimeZone);
        Assert.Equal(new IanaTimeZone("America/Phoenix"), plan.ItineraryDays[0].TimeZone);
        Assert.Equal(new PlanningDateRange(new(2026, 10, 3), new(2026, 10, 9)), plan.Dates);
        Assert.Equal(7, plan.ItineraryDays.Count);
        Assert.Contains(plan.ItineraryDays, day => day.Title == "Ride to Denver, Colorado");
        Assert.Contains(plan.ItineraryDays, day => day.Title == "Ride to Moab, Utah");
        Assert.Equal(new DateOnly(2026, 10, 3), plan.Transportation[0].ArrivalDate);
        Assert.Equal(new DateOnly(2026, 10, 7), plan.Transportation[3].DepartureDate);
        Assert.Equal(new DateOnly(2026, 10, 9), plan.Transportation[^1].ArrivalDate);
        Assert.Equal(4, plan.Accommodations.Count);
        Assert.All(plan.Accommodations, stay => Assert.StartsWith("Overnight near ", stay.Name));
        Assert.Equal(4, transaction.Reservation!.Fingerprint.Version);
    }

    /// <summary>Existing non-origin requests retain the historical fingerprint version for retry compatibility.</summary>
    [Fact]
    public async Task InstantiateAsync_ExistingTemplate_RetainsOriginalFingerprintVersion()
    {
        var transaction = new RecordingTransaction(Creator);

        var result = await Service(transaction).InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.Created, result.Outcome);
        Assert.Equal(1, transaction.Reservation!.Fingerprint.Version);
    }

    /// <summary>An origin-aware template fails before persistence when its required origin is absent.</summary>
    [Fact]
    public async Task InstantiateAsync_OriginAwareTemplateWithoutOrigin_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator);

        var result = await Service(transaction, new(OriginBlueprint(), "decision_origin_0001"))
            .InstantiateAsync(Command());

        Assert.Equal(AdventureTemplateInstantiateOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Added);
        Assert.False(transaction.Committed);
    }

    /// <summary>An origin-aware request cannot omit its reviewed distance assumptions.</summary>
    [Fact]
    public async Task InstantiateAsync_OriginAwareTemplateWithoutTravelEstimate_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator);
        var command = Command() with
        {
            ConfiguredOrigin = new("Phoenix, Arizona", new IanaTimeZone("America/Phoenix"))
        };

        var result = await Service(transaction, new(OriginBlueprint(), "decision_origin_0001"))
            .InstantiateAsync(command);

        Assert.Equal(AdventureTemplateInstantiateOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Added);
    }

    /// <summary>A multi-day route cannot be created without every reviewed overnight stop.</summary>
    [Fact]
    public async Task InstantiateAsync_OriginAwareTemplateWithoutTravelStops_FailsValidation()
    {
        var transaction = new RecordingTransaction(Creator);
        var command = Command() with
        {
            ConfiguredOrigin = new("Phoenix, Arizona", new IanaTimeZone("America/Phoenix")),
            TravelEstimate = new(1300, 450)
        };

        var result = await Service(transaction, new(OriginBlueprint(), "decision_origin_0001"))
            .InstantiateAsync(command);

        Assert.Equal(AdventureTemplateInstantiateOutcome.ValidationFailed, result.Outcome);
        Assert.Null(transaction.Repository.Added);
        Assert.False(transaction.Committed);
    }

    private static AdventureTemplateInstantiateCommand Command() => new(
        Actor, Creator, new PlanningIdempotencyKey("template-request-000001"),
        TemplateVersion, new DateOnly(2026, 10, 3), "en-US");

    private static AdventureTemplateInstantiateService Service(
        RecordingTransaction transaction,
        AuthorizedAdventureTemplateUse? use = null) => new(
        Membership(), new AllowEvaluator(), new StubUseResolver(use ?? Use()),
        new RecordingFactory(transaction), new FixedIdentities(), new FixedTimeProvider());

    private static AuthorizedAdventureTemplateUse Use() => new(Blueprint(), "decision_alpha_0001");

    private static AdventureTemplateBlueprint Blueprint() => new()
    {
        VersionId = TemplateVersion,
        OwnerType = AdventureTemplateOwnerType.Platform,
        OwnerId = "adventures-suite",
        SourceLocale = "en-US",
        Attribution = "AdventuresSuite curated Alpha collection",
        Title = "Portugal by rail",
        WorkingDescription = "An independent private starting point.",
        DurationDays = 4,
        Destinations =
        [
            new("lisbon", "Lisbon", 0, 1, new IanaTimeZone("Europe/Lisbon")),
            new("porto", "Porto", 2, 3, new IanaTimeZone("Europe/Lisbon"))
        ],
        Days =
        [
            new("arrival", 0, "lisbon", new IanaTimeZone("Europe/Lisbon"), "Arrive in Lisbon"),
            new("porto", 2, "porto", new IanaTimeZone("Europe/Lisbon"), "Porto riverfront")
        ],
        Activities = [new("porto", "Explore the riverfront")],
        Transportation =
        [
            new("Rail", "Lisbon", "Porto", 2, null, new IanaTimeZone("Europe/Lisbon"),
                2, null, new IanaTimeZone("Europe/Lisbon"))
        ],
        Accommodations =
        [
            new("Central Lisbon stay", 0, 1, new IanaTimeZone("Europe/Lisbon"))
        ]
    };

    private static AdventureTemplateBlueprint OriginBlueprint() => new()
    {
        VersionId = TemplateVersion,
        OwnerType = AdventureTemplateOwnerType.Creator,
        OwnerId = "creator_tsa_01",
        SourceLocale = "en-US",
        Attribution = "Reviewed test source",
        Title = "Sturgis motorcycle Journey",
        DurationDays = 3,
        Destinations =
        [
            new("origin-out", "Configured origin", 0, 0, new("UTC"), UsesConfiguredOrigin: true),
            new("sturgis", "Sturgis", 1, 1, new("America/Denver")),
            new("origin-back", "Configured origin", 2, 2, new("UTC"), UsesConfiguredOrigin: true)
        ],
        Days =
        [
            new("out", 0, "origin-out", new("UTC"), "Depart"),
            new("rally", 1, "sturgis", new("America/Denver"), "Rally"),
            new("back", 2, "origin-back", new("UTC"), "Return")
        ],
        Transportation =
        [
            new("Motorcycle", "Configured origin", "Sturgis", 0, null, new("UTC"),
                1, null, new("America/Denver"), "origin-out", "sturgis"),
            new("Motorcycle", "Sturgis", "Configured origin", 2, null, new("America/Denver"),
                2, null, new("UTC"), "sturgis", "origin-back")
        ]
    };

    private static ICreatorMembershipProvider Membership() => new StubMembershipProvider(new(
        new CreatorMembershipId("membership_template_01"), User, Creator,
        CreatorMembershipStatus.Active, [CreatorRole.Owner], [], 2, Now));

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId, CreatorId creatorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
    }

    private sealed class AllowEvaluator : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation));
    }

    private sealed class StubUseResolver(AuthorizedAdventureTemplateUse? use)
        : IAdventureTemplateUseResolver
    {
        public Task<AuthorizedAdventureTemplateUse?> ResolveAsync(
            ActorIdentity actor, CreatorId customerCreatorId,
            AdventureTemplateVersionId templateVersion, string requestedLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(use);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedIdentities : IPlanningCreationIdentityGenerator
    {
        private int visit;
        private int day;
        private int activity;
        private int transportation;
        private int accommodation;
        public AdventurePlanId NewAdventurePlanId() => new("plan_template_01");
        public DestinationVisitId NewDestinationVisitId() => new($"visit_new_{++visit:00}");
        public ItineraryDayId NewItineraryDayId() => new($"day_new_{++day:00}");
        public PlannedActivityId NewPlannedActivityId() => new($"activity_new_{++activity:00}");
        public TransportationSegmentId NewTransportationSegmentId() => new($"transport_new_{++transportation:00}");
        public AccommodationId NewAccommodationId() => new($"stay_new_{++accommodation:00}");
        public ReservationId NewReservationId() => new("reservation_unused_01");
        public AuditEventId NewAuditEventId() => new("audit_template_01");
        public CorrelationId NewCorrelationId() => new("correlation_template_01");
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

    private sealed class RecordingTransaction(CreatorId creatorId) : IPlanningTransaction
    {
        public CreatorId CreatorId { get; } = creatorId;
        public RecordingRepository Repository { get; } = new();
        public RecordingOriginStore Origins { get; } = new();
        public List<AuditEventIntent> Audits { get; } = [];
        public bool ThrowOnOrigin { get => Origins.ThrowOnAdd; set => Origins.ThrowOnAdd = value; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public AdventurePlanCreateReservation? Reservation { get; private set; }
        public AdventurePlanCreateIdempotencyResult IdempotencyResult { get; set; } =
            new(AdventurePlanCreateIdempotencyOutcome.Reserved, null, null);
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanTemplateOriginStore AdventurePlanTemplateOrigins => Origins;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency =>
            new IdempotencyStore(this);
        public IRequiredAuditIntentCollector RequiredAuditIntents => new AuditCollector(Audits);
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

        private sealed class IdempotencyStore(RecordingTransaction owner)
            : IAdventurePlanCreateIdempotencyStore
        {
            public Task<AdventurePlanCreateIdempotencyResult> ReserveAsync(
                CreatorId creatorId, AdventurePlanCreateReservation reservation,
                CancellationToken cancellationToken = default)
            {
                owner.Reservation = reservation;
                return Task.FromResult(owner.IdempotencyResult);
            }
        }
    }

    private sealed class AuditCollector(List<AuditEventIntent> items) : IRequiredAuditIntentCollector
    {
        public void AddRequired(AuditEventIntent auditEvent) => items.Add(auditEvent);
    }

    private sealed class RecordingOriginStore : IAdventurePlanTemplateOriginStore
    {
        public AdventurePlanTemplateOrigin? Added { get; private set; }
        public bool ThrowOnAdd { get; set; }
        public Task AddAsync(CreatorId creatorId, AdventurePlanTemplateOrigin origin,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("origin failure");
            Added = origin;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRepository : IAdventurePlanRepository
    {
        public AdventurePlan? Added { get; private set; }
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan, CancellationToken cancellationToken = default)
        { Added = plan; return Task.CompletedTask; }
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<AdventurePlan?> GetAsync(CreatorId c, AdventurePlanId p, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId c, CancellationToken x = default) => throw new NotSupportedException();
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
        public Task AddReservationAsync(CreatorId c, AdventurePlan p, Reservation r, long v, CancellationToken x = default) => throw new NotSupportedException();
    }
}
