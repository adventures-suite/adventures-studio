using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.DatabaseMigrator;
using AdventuresSuite.Planning.SqlServer;
using Microsoft.Data.SqlClient;
using TheSimontonAdventures.Web.Authorization;
using TheSimontonAdventures.Web.Creators;
using TheSimontonAdventures.Web.Planning;
using TheSimontonAdventures.Web.Planning.Persistence;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Proves durable, Creator-scoped Adventure Plan creation idempotency.</summary>
public sealed class AdventurePlanCreateIdempotencyIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 17, 0, 0, TimeSpan.Zero);

    /// <summary>Exercises concurrency, replay, isolation, validation, and atomic rollback.</summary>
    [Fact]
    public async Task CreateResults_RealSqlServer_AreAtomicAndCreatorScoped()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteIdempotencyTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

        try
        {
            DatabaseMigratorRunner.Migrate(connectionString);
            var factory = new SqlPlanningTransactionFactory(connectionString);
            var alpha = new CreatorId("creator_alpha");
            var beta = new CreatorId("creator_beta");
            var key = new PlanningIdempotencyKey("browser-submit-00000001");
            var fingerprint = Fingerprint("same-request");
            var actor = new ActorIdentity(
                ActorType.Human, "user_planner", new UserId("user_planner"));
            var service = CreateService(factory, alpha);
            var command = new ManualAdventurePlanCreateCommand(
                actor, alpha, key, "Concurrent plan", null,
                new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2));

            var first = service.CreateAsync(command);
            var second = service.CreateAsync(command);
            var outcomes = await Task.WhenAll(first, second);

            Assert.Contains(outcomes, result =>
                result.Outcome == ManualAdventurePlanCreateOutcome.Created);
            Assert.Contains(outcomes, result =>
                result.Outcome == ManualAdventurePlanCreateOutcome.Replayed);
            Assert.Single(outcomes.Select(result => result.AdventurePlanId).Distinct());
            await AssertCountsAsync(connectionString, alpha, key, plans: 1, audits: 1, results: 1);

            var replay = await service.CreateAsync(command);
            Assert.Equal(ManualAdventurePlanCreateOutcome.Replayed, replay.Outcome);
            Assert.Equal(outcomes[0].AdventurePlanId, replay.AdventurePlanId);
            await AssertCountsAsync(connectionString, alpha, key, plans: 1, audits: 1, results: 1);

            var conflict = await service.CreateAsync(command with { Title = "Changed request" });
            Assert.Equal(ManualAdventurePlanCreateOutcome.Conflict, conflict.Outcome);
            Assert.Null(conflict.AdventurePlanId);
            await AssertCountsAsync(connectionString, alpha, key, plans: 1, audits: 1, results: 1);

            var betaResult = await CreateAsync(
                factory, beta, key, fingerprint, new AdventurePlanId("plan_beta_independent"));
            Assert.Equal(AdventurePlanCreateIdempotencyOutcome.Reserved, betaResult.Outcome);
            await AssertCountsAsync(connectionString, beta, key, plans: 1, audits: 1, results: 1);
            await using (var transaction = await factory.BeginAsync(alpha))
            {
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                        beta,
                        Reservation(key, fingerprint, new AdventurePlanId("plan_cross_creator"))));
            }

            await AssertCommitValidationRollsBackAsync(factory, connectionString, alpha);
            await AssertPlanFailureRollsBackAsync(factory, connectionString, alpha, outcomes[0].AdventurePlanId!.Value);
            await AssertAuditFailureRollsBackAsync(factory, connectionString, alpha);
            await AssertIdempotencyFailureRollsBackAsync(factory, connectionString, alpha);
            await AssertCorruptReplayFailsClosedAsync(factory, connectionString, alpha);
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

    private static async Task<AdventurePlanCreateIdempotencyResult> CreateAsync(
        SqlPlanningTransactionFactory factory,
        CreatorId creatorId,
        PlanningIdempotencyKey key,
        PlanningRequestFingerprint fingerprint,
        AdventurePlanId proposedPlanId)
    {
        await using var transaction = await factory.BeginAsync(creatorId);
        var result = await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
            creatorId, Reservation(key, fingerprint, proposedPlanId));
        if (result.Outcome == AdventurePlanCreateIdempotencyOutcome.Reserved)
        {
            await transaction.AdventurePlans.AddAsync(
                creatorId, CreatePlan(creatorId, proposedPlanId));
            transaction.RequiredAuditIntents.AddRequired(Audit(creatorId, proposedPlanId));
        }

        await transaction.CommitAsync();
        return result;
    }

    private static ManualAdventurePlanCreateService CreateService(
        SqlPlanningTransactionFactory factory,
        CreatorId creatorId) => new(
        new FixedMembershipProvider(creatorId),
        new AllowedAuthorizationEvaluator(),
        factory,
        new GuidPlanningCreationIdentityGenerator(),
        new FixedTimeProvider());

    private static async Task AssertCommitValidationRollsBackAsync(
        SqlPlanningTransactionFactory factory,
        string connectionString,
        CreatorId creatorId)
    {
        var key = new PlanningIdempotencyKey("missing-plan-000000001");
        await using (var transaction = await factory.BeginAsync(creatorId))
        {
            await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                creatorId,
                Reservation(key, Fingerprint("missing-plan"),
                    new AdventurePlanId("plan_missing_for_result")));
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
        }

        await AssertCountsAsync(connectionString, creatorId, key, plans: 1, audits: 1, results: 0);
    }

    private static async Task AssertPlanFailureRollsBackAsync(
        SqlPlanningTransactionFactory factory,
        string connectionString,
        CreatorId creatorId,
        AdventurePlanId existingPlanId)
    {
        var key = new PlanningIdempotencyKey("plan-failure-000000001");
        await using (var transaction = await factory.BeginAsync(creatorId))
        {
            await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                creatorId, Reservation(key, Fingerprint("plan-failure"), existingPlanId));
            await Assert.ThrowsAsync<SqlException>(() => transaction.AdventurePlans.AddAsync(
                creatorId, CreatePlan(creatorId, existingPlanId)));
        }

        await AssertCountsAsync(connectionString, creatorId, key, plans: 1, audits: 1, results: 0);
    }

    private static async Task AssertAuditFailureRollsBackAsync(
        SqlPlanningTransactionFactory factory,
        string connectionString,
        CreatorId creatorId)
    {
        var duplicateAuditId = new AuditEventId("audit_duplicate_idempotency");
        var seedKey = new PlanningIdempotencyKey("audit-seed-00000000001");
        await CreateWithAuditIdAsync(
            factory, creatorId, seedKey, new AdventurePlanId("plan_audit_seed"), duplicateAuditId);

        var key = new PlanningIdempotencyKey("audit-failure-00000001");
        var planId = new AdventurePlanId("plan_audit_rollback");
        await using (var transaction = await factory.BeginAsync(creatorId))
        {
            await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                creatorId, Reservation(key, Fingerprint("audit-failure"), planId));
            await transaction.AdventurePlans.AddAsync(creatorId, CreatePlan(creatorId, planId));
            transaction.RequiredAuditIntents.AddRequired(Audit(creatorId, planId, duplicateAuditId));
            await Assert.ThrowsAsync<SqlException>(() => transaction.CommitAsync());
        }

        await AssertCountsAsync(connectionString, creatorId, key, plans: 2, audits: 2, results: 0);
    }

    private static async Task AssertIdempotencyFailureRollsBackAsync(
        SqlPlanningTransactionFactory factory,
        string connectionString,
        CreatorId creatorId)
    {
        await ExecuteAsync(connectionString, """
            CREATE TRIGGER planning.FailCreateResultInsert
            ON planning.AdventurePlanCreateResults
            INSTEAD OF INSERT
            AS THROW 51000, 'Expected idempotency persistence failure.', 1;
            """);
        var key = new PlanningIdempotencyKey("result-failure-0000001");
        var planId = new AdventurePlanId("plan_result_rollback");
        try
        {
            await using var transaction = await factory.BeginAsync(creatorId);
            await transaction.AdventurePlans.AddAsync(creatorId, CreatePlan(creatorId, planId));
            transaction.RequiredAuditIntents.AddRequired(Audit(creatorId, planId));
            await Assert.ThrowsAsync<SqlException>(() =>
                transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                    creatorId, Reservation(key, Fingerprint("result-failure"), planId)));
        }
        finally
        {
            await ExecuteAsync(connectionString, "DROP TRIGGER planning.FailCreateResultInsert;");
        }

        await AssertCountsAsync(connectionString, creatorId, key, plans: 2, audits: 2, results: 0);
    }

    private static async Task AssertCorruptReplayFailsClosedAsync(
        SqlPlanningTransactionFactory factory,
        string connectionString,
        CreatorId creatorId)
    {
        var key = new PlanningIdempotencyKey("corrupt-replay-00000001");
        var fingerprint = Fingerprint("corrupt-replay");
        var planId = new AdventurePlanId("plan_corrupt_replay");
        await CreateAsync(factory, creatorId, key, fingerprint, planId);
        await ExecuteAsync(connectionString, $"""
            DELETE FROM planning.AdventurePlans
             WHERE CreatorId='{creatorId.Value}' AND AdventurePlanId='{planId.Value}';
            """);

        await using var transaction = await factory.BeginAsync(creatorId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transaction.AdventurePlanCreateIdempotency.ReserveAsync(
                creatorId,
                Reservation(key, fingerprint, new AdventurePlanId("plan_corrupt_retry"))));

        var mismatchedKey = new PlanningIdempotencyKey("mismatch-replay-0000001");
        var mismatchedFingerprint = Fingerprint("mismatch-replay");
        var mismatchedPlanId = new AdventurePlanId("plan_mismatch_replay");
        await CreateAsync(factory, creatorId, mismatchedKey, mismatchedFingerprint, mismatchedPlanId);
        await ExecuteAsync(connectionString, $"""
            UPDATE planning.AdventurePlans SET Version=2
             WHERE CreatorId='{creatorId.Value}' AND AdventurePlanId='{mismatchedPlanId.Value}';
            """);

        await using var mismatchedTransaction = await factory.BeginAsync(creatorId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mismatchedTransaction.AdventurePlanCreateIdempotency.ReserveAsync(
                creatorId,
                Reservation(
                    mismatchedKey,
                    mismatchedFingerprint,
                    new AdventurePlanId("plan_mismatch_retry"))));
    }

    private static async Task CreateWithAuditIdAsync(
        SqlPlanningTransactionFactory factory,
        CreatorId creatorId,
        PlanningIdempotencyKey key,
        AdventurePlanId planId,
        AuditEventId auditEventId)
    {
        await using var transaction = await factory.BeginAsync(creatorId);
        await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
            creatorId, Reservation(key, Fingerprint("audit-seed"), planId));
        await transaction.AdventurePlans.AddAsync(creatorId, CreatePlan(creatorId, planId));
        transaction.RequiredAuditIntents.AddRequired(Audit(creatorId, planId, auditEventId));
        await transaction.CommitAsync();
    }

    private static AdventurePlanCreateReservation Reservation(
        PlanningIdempotencyKey key,
        PlanningRequestFingerprint fingerprint,
        AdventurePlanId planId) => new(
            PlanningIdempotencyOperations.AdventurePlanCreateV1,
            key,
            fingerprint,
            planId,
            1,
            Now,
            Now.AddDays(30));

    private static PlanningRequestFingerprint Fingerprint(string value) =>
        new(1, SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static AdventurePlan CreatePlan(CreatorId creatorId, AdventurePlanId planId) => new(
        planId,
        creatorId,
        "Retry-safe plan",
        workingDescription: null,
        AdventureLifecycleStage.Dream,
        PlanningStatus.Idea,
        new PlanningDateRange(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2)),
        new PlanAudit(1, Now, Now));

    private static AuditEventIntent Audit(
        CreatorId creatorId,
        AdventurePlanId planId,
        AuditEventId? auditEventId = null) => new(
            auditEventId ?? new AuditEventId($"audit_{Guid.NewGuid():N}"),
            new ActorIdentity(ActorType.Human, "actor_planner", new UserId("user_planner")),
            creatorId,
            Permissions.AdventurePlanCreate,
            AuthorizationResourceScope.ForInstance(
                creatorId, AuthorizationResourceTypes.AdventurePlan, planId.Value),
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            Now,
            new CorrelationId($"correlation_{Guid.NewGuid():N}"),
            previousVersion: null,
            resultingVersion: 1);

    private static async Task AssertCountsAsync(
        string connectionString,
        CreatorId creatorId,
        PlanningIdempotencyKey key,
        int plans,
        int audits,
        int results)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM planning.AdventurePlans WHERE CreatorId=@CreatorId),
              (SELECT COUNT(*) FROM audit.AuditEvents
                WHERE CreatorId=@CreatorId AND Permission='AdventurePlan.Create'),
              (SELECT COUNT(*) FROM planning.AdventurePlanCreateResults
                WHERE CreatorId=@CreatorId AND IdempotencyKey=@IdempotencyKey);
            """, connection);
        command.Parameters.AddWithValue("CreatorId", creatorId.Value);
        command.Parameters.AddWithValue("IdempotencyKey", key.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(plans, reader.GetInt32(0));
        Assert.Equal(audits, reader.GetInt32(1));
        Assert.Equal(results, reader.GetInt32(2));
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
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private sealed class FixedMembershipProvider(CreatorId creatorId)
        : ICreatorMembershipProvider
    {
        public Task<CreatorMembershipSnapshot?> GetMembershipAsync(
            UserId userId,
            CreatorId requestedCreatorId,
            CancellationToken cancellationToken = default) => Task.FromResult(
                requestedCreatorId == creatorId
                    ? new CreatorMembershipSnapshot(
                        new CreatorMembershipId("membership_planner"),
                        userId,
                        creatorId,
                        CreatorMembershipStatus.Active,
                        [CreatorRole.Owner],
                        [],
                        1,
                        Now)
                    : null);
    }

    private sealed class AllowedAuthorizationEvaluator : IAuthorizationPolicyEvaluator
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
                AuthorizationDecision.Allow(AuthorizationAuditRequirement.RequiredMutation));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
