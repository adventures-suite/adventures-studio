using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Identity;
using AdventuresSuite.Planning.SqlServer;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the Dapper Planning adapter against real SQL Server behavior.</summary>
public sealed class PlanningRepositoryIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";

    /// <summary>Proves round trips, Creator isolation, concurrency, and rollback.</summary>
    [Fact]
    public async Task Repository_RealSqlServer_PreservesPlanningBoundaries()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteRepositoryTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(databaseConnectionString);
            var factory = new SqlPlanningTransactionFactory(databaseConnectionString);
            var alpha = new CreatorId("creator_alpha");
            var beta = new CreatorId("creator_beta");
            var original = CreatePlan(alpha, 1, "Spain and Atlantic");
            var originalAuditId = new AuditEventId("audit_planning_create");

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha, original);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, original.Id, Permissions.AdventurePlanCreate, null, 1,
                    originalAuditId));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.GetAsync(beta, original.Id));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlans.AddAsync(alpha, CreatePlan(beta, 1, "Wrong owner")));
                await transaction.CommitAsync();
            }

            await AssertSuccessfulAuditAsync(
                databaseConnectionString, originalAuditId, alpha, original.Id);

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                AssertPlanGraph(original, loaded!);
                Assert.Single(await transaction.AdventurePlans.ListAsync(alpha));
                var dashboard = Assert.Single(
                    await transaction.AdventurePlans.ListDashboardAsync(alpha));
                Assert.Equal(original.Id, dashboard.Id);
                Assert.Equal(original.Audit.Version, dashboard.Version);
                Assert.False(dashboard.IsArchived);
                var facts = await transaction.AdventurePlans.GetAuthorizationFactsAsync(
                    alpha, original.Id);
                Assert.NotNull(facts);
                Assert.Equal(alpha, facts.CreatorId);
                Assert.Equal(original.Id, facts.PlanId);
                Assert.Equal(original.Audit.Version, facts.Version);
                Assert.False(facts.IsArchived);
                var detail = await transaction.AdventurePlans.GetDetailAsync(alpha, original.Id);
                Assert.NotNull(detail);
                Assert.Equal("Private working plan", detail.WorkingDescription);
                Assert.Equal(1, detail.TravelerCount);
                Assert.Equal("Madrid", Assert.Single(detail.Destinations).Name);
                Assert.Equal("Prado", Assert.Single(Assert.Single(detail.Days).Activities).Title);
                Assert.Equal("Flight", Assert.Single(detail.Transportation).Mode);
                Assert.Equal("Madrid hotel", Assert.Single(detail.Accommodations).Name);
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(beta))
            {
                Assert.Null(await transaction.AdventurePlans.GetAsync(beta, original.Id));
                Assert.Null(await transaction.AdventurePlans.GetAuthorizationFactsAsync(beta, original.Id));
                Assert.Null(await transaction.AdventurePlans.GetDetailAsync(beta, original.Id));
                var independent = CreatePlan(beta, 1, "Independent plan");
                await transaction.AdventurePlans.AddAsync(beta, independent);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    beta, independent.Id, Permissions.AdventurePlanCreate, null, 1));
                await transaction.CommitAsync();
            }

            var updated = CreatePlan(alpha, 2, "Updated Spain and Atlantic");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, updated, 1);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, updated.Id, Permissions.AdventurePlanEdit, 1, 2));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var stale = CreatePlan(alpha, 2, "Stale write");
                await Assert.ThrowsAsync<PlanningConcurrencyException>(() =>
                    transaction.AdventurePlans.UpdateAsync(alpha, stale, 1));
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var loaded = await transaction.AdventurePlans.GetAsync(alpha, original.Id);
                Assert.Equal("Updated Spain and Atlantic", loaded!.Title);
                Assert.Equal(2, loaded.Audit.Version);
                await transaction.CommitAsync();
            }

            var archived = CreatePlan(alpha, 3, "Archived Spain and Atlantic",
                lifecycleStage: AdventureLifecycleStage.Remember,
                status: PlanningStatus.Archived);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, archived, 2);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, archived.Id, Permissions.AdventurePlanArchive, 2, 3));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Empty(await transaction.AdventurePlans.ListAsync(alpha));
                Assert.Empty(await transaction.AdventurePlans.ListDashboardAsync(alpha));
                Assert.Equal(archived.Id, Assert.Single(
                    await transaction.AdventurePlans.ListArchivedAsync(alpha)).Id);
                Assert.Equal(PlanningStatus.Archived,
                    (await transaction.AdventurePlans.GetAsync(alpha, archived.Id))!.Status);
                await transaction.CommitAsync();
            }

            var restored = CreatePlan(alpha, 4, "Restored Spain and Atlantic",
                lifecycleStage: AdventureLifecycleStage.Remember,
                status: PlanningStatus.Completed);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.UpdateAsync(alpha, restored, 3);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, restored.Id, Permissions.AdventurePlanRestore, 3, 4));
                await transaction.CommitAsync();
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Equal(restored.Id, Assert.Single(
                    await transaction.AdventurePlans.ListAsync(alpha)).Id);
                Assert.Empty(await transaction.AdventurePlans.ListArchivedAsync(alpha));
                await transaction.CommitAsync();
            }

            var rollbackId = new AdventurePlanId("plan_rollback");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha,
                    CreatePlan(alpha, 1, "Rollback plan", rollbackId));
            }

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Null(await transaction.AdventurePlans.GetAsync(alpha, rollbackId));
                await transaction.CommitAsync();
            }

            var missingAuditId = new AdventurePlanId("plan_missing_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await transaction.AdventurePlans.AddAsync(alpha,
                    CreatePlan(alpha, 1, "Missing audit", missingAuditId));
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, missingAuditId, auditEventId: null);

            var mismatchedAuditPlanId = new AdventurePlanId("plan_mismatched_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var mismatchedAuditPlan = CreatePlan(
                    alpha, 1, "Mismatched audit", mismatchedAuditPlanId);
                await transaction.AdventurePlans.AddAsync(alpha, mismatchedAuditPlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, mismatchedAuditPlanId, Permissions.AdventurePlanEdit, null, 1));
                await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, mismatchedAuditPlanId, auditEventId: null);

            var duplicateAuditId = new AuditEventId("audit_duplicate_planning");
            var duplicateAuditPlanId = new AdventurePlanId("plan_duplicate_audit");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var duplicateAuditPlan = CreatePlan(alpha, 1, "Audit seed", duplicateAuditPlanId);
                await transaction.AdventurePlans.AddAsync(alpha, duplicateAuditPlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, duplicateAuditPlanId, Permissions.AdventurePlanCreate, null, 1,
                    duplicateAuditId));
                await transaction.CommitAsync();
            }

            var auditFailurePlanId = new AdventurePlanId("plan_audit_failure");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                var auditFailurePlan = CreatePlan(alpha, 1, "Audit failure", auditFailurePlanId);
                await transaction.AdventurePlans.AddAsync(alpha, auditFailurePlan);
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, auditFailurePlanId, Permissions.AdventurePlanCreate, null, 1,
                    duplicateAuditId));
                await Assert.ThrowsAsync<SqlException>(() => transaction.CommitAsync());
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, auditFailurePlanId, auditEventId: null);

            var planFailureAuditId = new AuditEventId("audit_plan_failure");
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                transaction.RequiredAuditIntents.AddRequired(Audit(
                    alpha, original.Id, Permissions.AdventurePlanCreate, null, 1,
                    planFailureAuditId));
                await Assert.ThrowsAsync<SqlException>(() => transaction.AdventurePlans.AddAsync(
                    alpha, CreatePlan(alpha, 1, "Duplicate plan")));
            }

            await AssertPlanAndAuditAbsentAsync(
                databaseConnectionString, alpha, new AdventurePlanId("plan_absent_probe"),
                planFailureAuditId);

            await using (var transaction = await factory.BeginAsync(alpha))
            {
                Assert.Throws<ArgumentException>(() => transaction.RequiredAuditIntents.AddRequired(
                    Audit(beta, original.Id, Permissions.AdventurePlanCreate, null, 1)));
            }
        }
        finally
        {
            await ExecuteAsync(masterConnectionString, $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;
                """);
        }
    }

    private static AuditEventIntent Audit(
        CreatorId creatorId,
        AdventurePlanId planId,
        Permission permission,
        long? previousVersion,
        long resultingVersion,
        AuditEventId? auditEventId = null) => new(
            auditEventId ?? new AuditEventId($"audit_{Guid.NewGuid():N}"),
            new ActorIdentity(ActorType.Human, "actor_planner", new UserId("user_planner")),
            creatorId,
            permission,
            AuthorizationResourceScope.ForInstance(
                creatorId, AuthorizationResourceTypes.AdventurePlan, planId.Value),
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            new DateTimeOffset(2026, 8, 11, 17, 0, 0, TimeSpan.Zero),
            new CorrelationId($"correlation_{Guid.NewGuid():N}"),
            previousVersion: previousVersion,
            resultingVersion: resultingVersion);

    private static async Task AssertPlanAndAuditAbsentAsync(
        string connectionString,
        CreatorId creatorId,
        AdventurePlanId planId,
        AuditEventId? auditEventId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM planning.AdventurePlans
               WHERE CreatorId=@CreatorId AND AdventurePlanId=@PlanId),
              (SELECT COUNT(*) FROM audit.AuditEvents
               WHERE AuditEventId=@AuditEventId);
            """, connection);
        command.Parameters.AddWithValue("CreatorId", creatorId.Value);
        command.Parameters.AddWithValue("PlanId", planId.Value);
        command.Parameters.AddWithValue("AuditEventId", auditEventId?.Value ?? "audit_absent_probe");
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
    }

    private static async Task AssertSuccessfulAuditAsync(
        string connectionString,
        AuditEventId auditEventId,
        CreatorId creatorId,
        AdventurePlanId planId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT CreatorId,ActorType,ActorUserId,Permission,ResourceType,ResourceId,
                   Outcome,ReasonCategory,OccurredAtUtc,CorrelationId,PreviousVersion,ResultingVersion
            FROM audit.AuditEvents
            WHERE AuditEventId=@AuditEventId;
            """, connection);
        command.Parameters.AddWithValue("AuditEventId", auditEventId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(creatorId.Value, reader.GetString(0));
        Assert.Equal(nameof(ActorType.Human), reader.GetString(1));
        Assert.Equal("user_planner", reader.GetString(2));
        Assert.Equal(Permissions.AdventurePlanCreate.Value, reader.GetString(3));
        Assert.Equal(AuthorizationResourceTypes.AdventurePlan.Value, reader.GetString(4));
        Assert.Equal(planId.Value, reader.GetString(5));
        Assert.Equal(nameof(AuditOutcome.Succeeded), reader.GetString(6));
        Assert.Equal(nameof(AuditReasonCategory.Completed), reader.GetString(7));
        Assert.Equal(TimeSpan.Zero, reader.GetFieldValue<DateTimeOffset>(8).Offset);
        Assert.StartsWith("correlation_", reader.GetString(9), StringComparison.Ordinal);
        Assert.True(reader.IsDBNull(10));
        Assert.Equal(1, reader.GetInt64(11));
    }

    private static AdventurePlan CreatePlan(
        CreatorId creatorId,
        long version,
        string title,
        AdventurePlanId? id = null,
        AdventureLifecycleStage lifecycleStage = AdventureLifecycleStage.Plan,
        PlanningStatus status = PlanningStatus.Planned)
    {
        var created = new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);
        var visitId = new DestinationVisitId("visit_madrid");
        var dayId = new ItineraryDayId("day_madrid");
        return new AdventurePlan(
            id ?? new AdventurePlanId("plan_shared"), creatorId, title, "Private working plan",
            lifecycleStage, status,
            new(new DateOnly(2027, 10, 25), new DateOnly(2027, 11, 15)),
            new(version, created, created.AddMinutes(version - 1)),
            [new Traveler { Id = new("traveler_steve"), DisplayName = "Steve", Preferences = ["Window seat"] }],
            [new DestinationVisit { Id = visitId, Name = "Madrid", Dates = new(new(2027, 10, 26), new(2027, 10, 29)), TimeZone = new("Europe/Madrid"), Sequence = 1, Notes = "Explore" }],
            [new ItineraryDay { Id = dayId, Date = new(2027, 10, 26), TimeZone = new("Europe/Madrid"), DestinationVisitId = visitId, Title = "Madrid arrival" }],
            [new PlannedActivity { Id = new("activity_prado"), ItineraryDayId = dayId, Title = "Prado", StartsAtLocal = new(10, 0), EndsAtLocal = new(12, 0), Status = PlanItemStatus.Confirmed }],
            [new TransportationSegment { Id = new("transport_flight"), Mode = "Flight", From = "Phoenix", To = "Madrid", DepartureDate = new(2027, 10, 25), DepartureTimeLocal = new(18, 0), DepartureTimeZone = new("America/Phoenix"), ArrivalDate = new(2027, 10, 26), ArrivalTimeLocal = new(13, 0), ArrivalTimeZone = new("Europe/Madrid"), Status = PlanItemStatus.Reserved }],
            [new Accommodation { Id = new("accommodation_madrid"), Name = "Madrid hotel", Dates = new(new(2027, 10, 26), new(2027, 10, 29)), TimeZone = new("Europe/Madrid"), Status = PlanItemStatus.Reserved }],
            [new Reservation { Id = new("reservation_prado"), Subject = "Prado entry", ConfirmationReference = "ABC123", Status = PlanItemStatus.Confirmed }],
            [new PlanningNote { Id = new("note_madrid"), Text = "Walkable neighborhoods" }],
            [new PlanningTask { Id = new("task_insurance"), Description = "Review insurance", DueDate = new(2027, 8, 1), IsCompleted = false }],
            [new BudgetItem { Id = new("budget_flight"), Description = "Flights", Amount = 2500m, CurrencyCode = "USD" }],
            [new PackingItem { Id = new("packing_adapter"), Description = "Power adapter", IsPacked = true }]);
    }

    private static void AssertPlanGraph(AdventurePlan expected, AdventurePlan actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CreatorId, actual.CreatorId);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Audit, actual.Audit);
        Assert.Single(actual.Travelers);
        Assert.Equal("Window seat", Assert.Single(actual.Travelers[0].Preferences));
        Assert.Single(actual.DestinationVisits);
        Assert.Single(actual.ItineraryDays);
        Assert.Single(actual.Activities);
        Assert.Single(actual.Transportation);
        Assert.Single(actual.Accommodations);
        Assert.Single(actual.Reservations);
        Assert.Single(actual.Notes);
        Assert.Single(actual.Tasks);
        Assert.Single(actual.BudgetItems);
        Assert.Single(actual.PackingItems);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildDatabaseConnectionString(string masterConnectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }
}
