using AdventuresSuite.Identity;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies authorization, concurrency, and atomic overview-edit behavior.</summary>
public sealed class AdventurePlanOverviewEditServiceTests
{
    private static readonly UserId User = new("user_planner_01");
    private static readonly CreatorId Creator = new("creator_alpha_01");
    private static readonly AdventurePlanId PlanId = new("plan_overview_01");
    private static readonly ActorIdentity Actor = new(ActorType.Human, User.Value, User);
    private static readonly DateTimeOffset Created =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A valid edit preserves non-overview state and commits exact audit versions.</summary>
    [Fact]
    public async Task EditAsync_ValidOverview_CommitsRootOnlyUpdateAndAudit()
    {
        var current = Plan();
        var transaction = new RecordingTransaction(Creator, current);
        var authorization = new RecordingAuthorizationEvaluator(Allowed());

        var result = await Service(transaction, authorization).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Updated, result.Outcome);
        Assert.Equal(2, result.Version);
        Assert.True(transaction.Committed);
        var updated = Assert.IsType<AdventurePlan>(transaction.Repository.Updated);
        Assert.Equal("Updated title", updated.Title);
        Assert.Equal("Updated description", updated.WorkingDescription);
        Assert.Equal(current.LifecycleStage, updated.LifecycleStage);
        Assert.Equal(current.Status, updated.Status);
        Assert.Equal(Created, updated.Audit.CreatedAtUtc);
        Assert.Equal(Now, updated.Audit.UpdatedAtUtc);
        Assert.Equal(1, transaction.Repository.ExpectedVersion);
        var audit = Assert.Single(transaction.Audits.Items);
        Assert.Equal(Permissions.AdventurePlanEdit, audit.Permission);
        Assert.Equal(1, audit.PreviousVersion);
        Assert.Equal(2, audit.ResultingVersion);
        Assert.Equal(PlanId.Value, audit.Resource.ResourceId);
        Assert.Equal(AuthorizationResourceScopeType.ResourceInstance,
            authorization.LastRequest!.Resource.ScopeType);
    }

    /// <summary>Denied authorization cannot begin mutation persistence.</summary>
    [Fact]
    public async Task EditAsync_Denied_DoesNotBeginTransaction()
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var service = new AdventurePlanOverviewEditService(
            Membership(),
            new RecordingAuthorizationEvaluator(AuthorizationDecision.Deny(
                AuthorizationDenialReason.PermissionRequired)),
            factory,
            new FixedIdentities(),
            new FixedTimeProvider());

        var result = await service.EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Denied, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Cross-Creator attempts cannot authorize or probe Planning persistence.</summary>
    [Fact]
    public async Task EditAsync_CrossCreatorRequest_IsNonDisclosing()
    {
        var authorization = new RecordingAuthorizationEvaluator(Allowed());
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var service = new AdventurePlanOverviewEditService(
            new StubMembershipProvider(null), authorization, factory,
            new FixedIdentities(), new FixedTimeProvider());

        var result = await service.EditAsync(Command() with
        {
            CreatorId = new CreatorId("creator_other_01")
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.Denied, result.Outcome);
        Assert.Null(authorization.LastRequest);
        Assert.Equal(0, factory.BeginCount);
    }

    /// <summary>Ownership mismatch and missing plans share the same safe outcome.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditAsync_MissingOrMismatchedPlan_IsDenied(bool mismatched)
    {
        var current = mismatched
            ? Plan(new CreatorId("creator_other_01"))
            : null;
        var transaction = new RecordingTransaction(Creator, current);

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Denied, result.Outcome);
        Assert.Null(transaction.Repository.Updated);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>An archived plan is denied when transaction-time state differs from authorization facts.</summary>
    [Fact]
    public async Task EditAsync_ArchivedInsideTransaction_IsDenied()
    {
        var archived = new AdventurePlan(
            PlanId, Creator, "Original title", "Original description",
            AdventureLifecycleStage.Remember, PlanningStatus.Archived,
            new(new(2027, 1, 1), new(2027, 1, 5)), new(1, Created, Created));
        var transaction = new RecordingTransaction(Creator, archived);

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Denied, result.Outcome);
        Assert.Null(transaction.Repository.Updated);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>Stale forms conflict before no-op detection or mutation.</summary>
    [Fact]
    public async Task EditAsync_StaleExpectedVersion_ConflictsWithoutMutation()
    {
        var transaction = new RecordingTransaction(Creator, Plan(version: 2));

        var result = await Service(transaction).EditAsync(Command() with
        {
            Title = "Original title",
            WorkingDescription = "Original description"
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.Conflict, result.Outcome);
        Assert.Null(transaction.Repository.Updated);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>An unchanged current form performs no version increment or audit.</summary>
    [Fact]
    public async Task EditAsync_Unchanged_ReturnsCurrentVersionWithoutMutation()
    {
        var transaction = new RecordingTransaction(Creator, Plan());

        var result = await Service(transaction).EditAsync(Command() with
        {
            Title = "Original title",
            WorkingDescription = "Original description"
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.Unchanged, result.Outcome);
        Assert.Equal(1, result.Version);
        Assert.Null(transaction.Repository.Updated);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>Childless plans may change their inclusive date range.</summary>
    [Fact]
    public async Task EditAsync_ChildlessDateChange_Succeeds()
    {
        var transaction = new RecordingTransaction(Creator, Plan());

        var result = await Service(transaction).EditAsync(Command() with
        {
            StartDate = new DateOnly(2027, 2, 1),
            EndDate = new DateOnly(2027, 2, 8)
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.Updated, result.Outcome);
        Assert.Equal(new PlanningDateRange(new(2027, 2, 1), new(2027, 2, 8)),
            transaction.Repository.Updated!.Dates);
    }

    /// <summary>Any date-bound itinerary data blocks a plan-range change.</summary>
    [Theory]
    [InlineData("visit")]
    [InlineData("day")]
    [InlineData("transport")]
    [InlineData("stay")]
    public async Task EditAsync_PopulatedPlanDateChange_IsBlocked(string child)
    {
        var transaction = new RecordingTransaction(Creator, PopulatedPlan(child));

        var result = await Service(transaction).EditAsync(Command() with
        {
            StartDate = new DateOnly(2027, 1, 2)
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.DateChangeBlocked, result.Outcome);
        Assert.Null(transaction.Repository.Updated);
        Assert.Empty(transaction.Audits.Items);
    }

    /// <summary>Title and description edits preserve populated itinerary records.</summary>
    [Fact]
    public async Task EditAsync_PopulatedPlanTextEdit_PreservesEveryChild()
    {
        var current = CompletePlan();
        var transaction = new RecordingTransaction(Creator, current);

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Updated, result.Outcome);
        var updated = transaction.Repository.Updated!;
        Assert.Equal(current.Travelers, updated.Travelers);
        Assert.Equal(current.DestinationVisits, updated.DestinationVisits);
        Assert.Equal(current.ItineraryDays, updated.ItineraryDays);
        Assert.Equal(current.Activities, updated.Activities);
        Assert.Equal(current.Transportation, updated.Transportation);
        Assert.Equal(current.Accommodations, updated.Accommodations);
        Assert.Equal(current.Reservations, updated.Reservations);
        Assert.Equal(current.Notes, updated.Notes);
        Assert.Equal(current.Tasks, updated.Tasks);
        Assert.Equal(current.BudgetItems, updated.BudgetItems);
        Assert.Equal(current.PackingItems, updated.PackingItems);
    }

    /// <summary>Concurrent duplicate submissions yield one update and one stale conflict.</summary>
    [Fact]
    public async Task EditAsync_DuplicateSubmission_UpdatesOnceThenConflicts()
    {
        var first = new RecordingTransaction(Creator, Plan());
        var second = new RecordingTransaction(Creator, Plan(version: 2));
        var service = Service(new RecordingFactory(first, second));

        var initial = await service.EditAsync(Command());
        var duplicate = await service.EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Updated, initial.Outcome);
        Assert.Equal(EditAdventurePlanOverviewOutcome.Conflict, duplicate.Outcome);
        Assert.Single(first.Audits.Items);
        Assert.Empty(second.Audits.Items);
    }

    /// <summary>An update conflict maps to a safe typed outcome.</summary>
    [Fact]
    public async Task EditAsync_ConcurrentUpdateConflict_DoesNotAppendAudit()
    {
        var transaction = new RecordingTransaction(Creator, Plan()) { ThrowConcurrency = true };

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Conflict, result.Outcome);
        Assert.Empty(transaction.Audits.Items);
        Assert.False(transaction.Committed);
    }

    /// <summary>Update or audit failures prevent transaction commit in either direction.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task EditAsync_UpdateOrAuditFailure_RollsBack(bool failUpdate, bool failAudit)
    {
        var transaction = new RecordingTransaction(Creator, Plan())
        {
            ThrowUpdate = failUpdate,
            ThrowAudit = failAudit
        };

        var result = await Service(transaction).EditAsync(Command());

        Assert.Equal(EditAdventurePlanOverviewOutcome.Failed, result.Outcome);
        Assert.False(transaction.Committed);
        Assert.True(transaction.Disposed);
    }

    /// <summary>Invalid overview fields are rejected after authorization and before mutation.</summary>
    [Theory]
    [InlineData("", "Description", "2027-01-01", "2027-01-05")]
    [InlineData(" Padded", "Description", "2027-01-01", "2027-01-05")]
    [InlineData("Title", " Padded", "2027-01-01", "2027-01-05")]
    [InlineData("Title", "Description", "2027-01-06", "2027-01-05")]
    public async Task EditAsync_InvalidFields_DoNotBeginMutation(
        string title, string description, string start, string end)
    {
        var factory = new RecordingFactory(new RecordingTransaction(Creator, Plan()));
        var result = await Service(factory).EditAsync(Command() with
        {
            Title = title,
            WorkingDescription = description,
            StartDate = DateOnly.Parse(start),
            EndDate = DateOnly.Parse(end)
        });

        Assert.Equal(EditAdventurePlanOverviewOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, factory.BeginCount);
    }

    private static EditAdventurePlanOverviewCommand Command() => new(
        Actor, Creator, PlanId, 1, "Updated title", "Updated description",
        new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 5));

    private static AdventurePlanOverviewEditService Service(
        RecordingTransaction transaction,
        RecordingAuthorizationEvaluator? authorization = null) =>
        Service(new RecordingFactory(transaction), authorization);

    private static AdventurePlanOverviewEditService Service(
        RecordingFactory factory,
        RecordingAuthorizationEvaluator? authorization = null) => new(
        Membership(), authorization ?? new RecordingAuthorizationEvaluator(Allowed()),
        factory, new FixedIdentities(), new FixedTimeProvider());

    private static AuthorizationDecision Allowed() =>
        AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation);

    private static ICreatorMembershipProvider Membership() => new StubMembershipProvider(new(
        new CreatorMembershipId("membership_planner_01"), User, Creator,
        CreatorMembershipStatus.Active, [CreatorRole.Owner], [], 4, Created));

    private static AdventurePlan Plan(CreatorId? creator = null, long version = 1) => new(
        PlanId, creator ?? Creator, "Original title", "Original description",
        AdventureLifecycleStage.Plan, PlanningStatus.Draft,
        new PlanningDateRange(new(2027, 1, 1), new(2027, 1, 5)),
        new PlanAudit(version, Created, version == 1 ? Created : Now));

    private static AdventurePlan PopulatedPlan(string child)
    {
        var visit = new DestinationVisit
        {
            Id = new("visit_01"),
            Name = "Phoenix",
            Dates = new(new(2027, 1, 1), new(2027, 1, 5)),
            TimeZone = new("America/Phoenix"),
            Sequence = 1
        };
        var day = new ItineraryDay
        {
            Id = new("day_01"),
            Date = new(2027, 1, 2),
            TimeZone = new("America/Phoenix"),
            Title = "Arrival"
        };
        var transport = new TransportationSegment
        {
            Id = new("transport_01"),
            Mode = "Rail",
            From = "A",
            To = "B",
            DepartureDate = new(2027, 1, 2),
            ArrivalDate = new(2027, 1, 2),
            DepartureTimeZone = new("America/Phoenix"),
            ArrivalTimeZone = new("America/Phoenix")
        };
        var stay = new Accommodation
        {
            Id = new("stay_01"),
            Name = "Hotel",
            Dates = new(new(2027, 1, 2), new(2027, 1, 3)),
            TimeZone = new("America/Phoenix")
        };
        return new AdventurePlan(
            PlanId, Creator, "Original title", "Original description",
            AdventureLifecycleStage.Plan, PlanningStatus.Draft,
            new(new(2027, 1, 1), new(2027, 1, 5)), new(1, Created, Created),
            destinationVisits: child == "visit" ? [visit] : [],
            itineraryDays: child == "day" ? [day] : [],
            transportation: child == "transport" ? [transport] : [],
            accommodations: child == "stay" ? [stay] : []);
    }

    private static AdventurePlan CompletePlan()
    {
        var visit = new DestinationVisit
        {
            Id = new("visit_01"),
            Name = "Phoenix",
            Dates = new(new(2027, 1, 1), new(2027, 1, 5)),
            TimeZone = new("America/Phoenix"),
            Sequence = 1
        };
        var day = new ItineraryDay
        {
            Id = new("day_01"),
            Date = new(2027, 1, 2),
            TimeZone = new("America/Phoenix"),
            DestinationVisitId = visit.Id,
            Title = "Arrival"
        };
        return new AdventurePlan(
            PlanId, Creator, "Original title", "Original description",
            AdventureLifecycleStage.Plan, PlanningStatus.Draft,
            new(new(2027, 1, 1), new(2027, 1, 5)), new(1, Created, Created),
            travelers: [new Traveler { Id = new("traveler_01"), DisplayName = "Traveler" }],
            destinationVisits: [visit], itineraryDays: [day],
            activities: [new PlannedActivity { Id = new("activity_01"), ItineraryDayId = day.Id, Title = "Walk" }],
            transportation: [new TransportationSegment
            {
                Id = new("transport_01"), Mode = "Rail", From = "A", To = "B",
                DepartureDate = day.Date, ArrivalDate = day.Date,
                DepartureTimeZone = day.TimeZone, ArrivalTimeZone = day.TimeZone
            }],
            accommodations: [new Accommodation { Id = new("stay_01"), Name = "Hotel", Dates = visit.Dates, TimeZone = visit.TimeZone }],
            reservations: [new Reservation { Id = new("reservation_01"), Subject = "Room" }],
            notes: [new PlanningNote { Id = new("note_01"), Text = "Private" }],
            tasks: [new PlanningTask { Id = new("task_01"), Description = "Pack" }],
            budgetItems: [new BudgetItem { Id = new("budget_01"), Description = "Rail", Amount = 1, CurrencyCode = "USD" }],
            packingItems: [new PackingItem { Id = new("packing_01"), Description = "Shoes" }]);
    }

    private sealed class StubMembershipProvider(CreatorMembershipSnapshot? membership)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(UserId userId, CreatorId creatorId,
            CancellationToken cancellationToken = default) => Task.FromResult(membership);
    }

    private sealed class RecordingAuthorizationEvaluator(AuthorizationDecision decision)
        : IAuthorizationPolicyEvaluator
    {
        public AuthorizationRequest? LastRequest { get; private set; }
        public Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(decision);
        }
    }

    private sealed class FixedIdentities : IPlanningCreationIdentityGenerator
    {
        public AdventurePlanId NewAdventurePlanId() => throw new InvalidOperationException();
        public DestinationVisitId NewDestinationVisitId() => throw new InvalidOperationException();
        public ItineraryDayId NewItineraryDayId() => throw new InvalidOperationException();
        public PlannedActivityId NewPlannedActivityId() => throw new InvalidOperationException();
        public TransportationSegmentId NewTransportationSegmentId() => throw new InvalidOperationException();
        public AuditEventId NewAuditEventId() => new("audit_edit_01");
        public CorrelationId NewCorrelationId() => new("correlation_edit_01");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingFactory(params RecordingTransaction[] transactions)
        : IPlanningTransactionFactory
    {
        private int index;
        public int BeginCount { get; private set; }
        public Task<IPlanningTransaction> BeginAsync(CreatorId creatorId,
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            var transaction = transactions[Math.Min(index++, transactions.Length - 1)];
            Assert.Equal(transaction.CreatorId, creatorId);
            return Task.FromResult<IPlanningTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction : IPlanningTransaction
    {
        public RecordingTransaction(CreatorId creatorId, AdventurePlan? current)
        {
            CreatorId = creatorId;
            Repository = new RecordingRepository(current, this);
        }
        public CreatorId CreatorId { get; }
        public RecordingRepository Repository { get; }
        public RecordingAuditCollector Audits { get; } = new();
        public bool ThrowConcurrency { get; set; }
        public bool ThrowUpdate { get; set; }
        public bool ThrowAudit { get => Audits.Throw; set => Audits.Throw = value; }
        public bool Committed { get; private set; }
        public bool Disposed { get; private set; }
        public IAdventurePlanRepository AdventurePlans => Repository;
        public IAdventurePlanCreateIdempotencyStore AdventurePlanCreateIdempotency =>
            throw new InvalidOperationException("Edits must not use creation idempotency.");
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
        public bool Throw { get; set; }
        public void AddRequired(AuditEventIntent auditEvent)
        {
            if (Throw) throw new InvalidOperationException("audit failure");
            Items.Add(auditEvent);
        }
    }

    private sealed class RecordingRepository(
        AdventurePlan? current,
        RecordingTransaction owner) : IAdventurePlanRepository
    {
        public AdventurePlan? Updated { get; private set; }
        public long ExpectedVersion { get; private set; }
        public Task<AdventurePlan?> GetAsync(CreatorId creatorId, AdventurePlanId planId,
            CancellationToken cancellationToken = default) => Task.FromResult(current);
        public Task UpdateOverviewAsync(CreatorId creatorId, AdventurePlan plan,
            long expectedVersion, CancellationToken cancellationToken = default)
        {
            if (owner.ThrowConcurrency)
                throw new PlanningConcurrencyException(plan.Id, expectedVersion);
            if (owner.ThrowUpdate) throw new InvalidOperationException("update failure");
            Updated = plan;
            ExpectedVersion = expectedVersion;
            return Task.CompletedTask;
        }
        public Task AddDestinationVisitAsync(CreatorId creatorId, AdventurePlan plan,
            DestinationVisit destinationVisit, long expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddItineraryDayAsync(CreatorId creatorId, AdventurePlan plan,
            ItineraryDay itineraryDay, long expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddPlannedActivityAsync(CreatorId creatorId, AdventurePlan plan,
            PlannedActivity activity, long expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddTransportationSegmentAsync(CreatorId creatorId, AdventurePlan plan,
            TransportationSegment segment, long expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdventurePlanAuthorizationFacts?> GetAuthorizationFactsAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlanDashboardItem>> ListDashboardAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AdventurePlanDetail?> GetDetailAsync(CreatorId creatorId, AdventurePlanId planId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AdventurePlan>> ListArchivedAsync(CreatorId creatorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(CreatorId creatorId, AdventurePlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(CreatorId creatorId, AdventurePlan plan, long expectedVersion, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Overview edits must not replace aggregate children.");
    }
}
