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

/// <summary>Proves template provenance and plan creation share one SQL transaction.</summary>
public sealed class AdventureTemplateOriginIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private static readonly CreatorId Creator = new("creator_template_sql");
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Exercises success, replay, missing-origin, and failed-origin rollback.</summary>
    [Fact]
    public async Task TemplateOrigin_RealSqlServer_IsAtomicAppendOnlyAndReplaySafe()
    {
        var master = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(master),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteTemplateOrigin_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString);
            var factory = new SqlPlanningTransactionFactory(connectionString);
            var key = new PlanningIdempotencyKey("template-sql-request-0001");
            var planId = new AdventurePlanId("plan_template_sql_01");

            await CreateAsync(factory, key, planId, includeOrigin: true);
            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM planning.AdventurePlans WHERE CreatorId=@CreatorId;"));
            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM planning.AdventurePlanTemplateOrigins WHERE CreatorId=@CreatorId;"));
            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE CreatorId=@CreatorId;"));

            await using (var replay = await factory.BeginAsync(Creator))
            {
                var result = await replay.AdventurePlanCreateIdempotency.ReserveAsync(
                    Creator, Reservation(key, planId));
                Assert.Equal(AdventurePlanCreateIdempotencyOutcome.Replay, result.Outcome);
            }

            var missingKey = new PlanningIdempotencyKey("template-sql-missing-0001");
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await CreateAsync(factory, missingKey,
                    new AdventurePlanId("plan_template_missing"), includeOrigin: false));
            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM planning.AdventurePlans WHERE CreatorId=@CreatorId;"));

            await ExecuteAsync(connectionString, """
                CREATE TRIGGER planning.FailTemplateOriginInsert
                ON planning.AdventurePlanTemplateOrigins
                INSTEAD OF INSERT
                AS THROW 51000, 'Expected template-origin failure.', 1;
                """);
            try
            {
                await Assert.ThrowsAsync<SqlException>(async () =>
                    await CreateAsync(factory,
                        new PlanningIdempotencyKey("template-sql-failure-0001"),
                        new AdventurePlanId("plan_template_failure"), includeOrigin: true));
            }
            finally
            {
                await ExecuteAsync(connectionString,
                    "DROP TRIGGER planning.FailTemplateOriginInsert;");
            }

            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM planning.AdventurePlans WHERE CreatorId=@CreatorId;"));
            Assert.Equal(1, await CountAsync(connectionString,
                "SELECT COUNT(*) FROM planning.AdventurePlanCreateResults WHERE CreatorId=@CreatorId;"));
        }
        finally
        {
            await ExecuteAsync(master, $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;
                """);
        }
    }

    private static async Task CreateAsync(
        SqlPlanningTransactionFactory factory,
        PlanningIdempotencyKey key,
        AdventurePlanId planId,
        bool includeOrigin)
    {
        await using var transaction = await factory.BeginAsync(Creator);
        var reservation = await transaction.AdventurePlanCreateIdempotency.ReserveAsync(
            Creator, Reservation(key, planId));
        Assert.Equal(AdventurePlanCreateIdempotencyOutcome.Reserved, reservation.Outcome);
        await transaction.AdventurePlans.AddAsync(Creator, new AdventurePlan(
            planId, Creator, "Template plan", null, AdventureLifecycleStage.Plan,
            PlanningStatus.Draft,
            new PlanningDateRange(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 2)),
            new PlanAudit(1, Now, Now)));
        if (includeOrigin)
        {
            await transaction.AdventurePlanTemplateOrigins.AddAsync(
                Creator, Origin(planId));
        }

        transaction.RequiredAuditIntents.AddRequired(new AuditEventIntent(
            new AuditEventId($"audit_{planId.Value}"),
            new AdventuresSuite.Identity.ActorIdentity(
                AdventuresSuite.Identity.ActorType.Human,
                "user_template_sql",
                new AdventuresSuite.Identity.UserId("user_template_sql")),
            Creator,
            Permissions.AdventurePlanCreate,
            AuthorizationResourceScope.ForInstance(
                Creator, AuthorizationResourceTypes.AdventurePlan, planId.Value),
            AuditOutcome.Succeeded,
            AuditReasonCategory.Completed,
            Now,
            new CorrelationId($"correlation_{planId.Value}"),
            previousVersion: null,
            resultingVersion: 1));
        await transaction.CommitAsync();
    }

    private static AdventurePlanCreateReservation Reservation(
        PlanningIdempotencyKey key,
        AdventurePlanId planId) => new(
            PlanningIdempotencyOperations.AdventurePlanTemplateInstantiateV1,
            key,
            Fingerprint(key.Value),
            planId,
            1,
            Now,
            Now.AddDays(30));

    private static AdventurePlanTemplateOrigin Origin(AdventurePlanId planId) => new()
    {
        CreatorId = Creator,
        AdventurePlanId = planId,
        TemplateVersion = new AdventureTemplateVersionId("template_sql", "1.0"),
        TemplateOwnerType = AdventureTemplateOwnerType.Platform,
        TemplateOwnerId = "adventures-suite",
        SourceLocale = "en-US",
        Attribution = "AdventuresSuite test collection",
        UseDecisionReference = "decision_sql_0001",
        ParameterFingerprint = Fingerprint("parameters"),
        InstantiatedAtUtc = Now
    };

    private static PlanningRequestFingerprint Fingerprint(string value) => new(
        1, SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<int> CountAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CreatorId", Creator.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string master, string database)
    {
        var builder = new SqlConnectionStringBuilder(master)
        {
            InitialCatalog = database,
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
