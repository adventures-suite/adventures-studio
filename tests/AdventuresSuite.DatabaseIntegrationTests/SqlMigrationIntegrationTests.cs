using System.Data;
using System.Security.Cryptography;
using System.Text;
using AdventuresSuite.DatabaseMigrator;
using DbUp;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies real DbUp behavior against Microsoft SQL Server.</summary>
public sealed class SqlMigrationIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";

    /// <summary>Runs the complete disposable-database migration and constraint gate.</summary>
    [Fact]
    public async Task Migrations_RealSqlServer_PassAuthoritativeGate()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");

        var databaseName = $"AdventuresSuiteMigrationTest_{Guid.NewGuid():N}";
        var databaseConnectionString = BuildDatabaseConnectionString(
            masterConnectionString,
            databaseName);

        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var firstRun = await CompanionPolicyMigrationTestHarness.MigrateAllAsync(databaseConnectionString);
            Assert.Equal(12, firstRun.Count);

            await VerifySchemaAsync(databaseConnectionString);
            await VerifyConstraintsAsync(databaseConnectionString);
            await VerifyRuntimePermissionsAsync(databaseConnectionString);
            var signatureBefore = await GetSchemaSignatureAsync(databaseConnectionString);

            var secondRun = await CompanionPolicyMigrationTestHarness.MigrateAllAsync(databaseConnectionString);

            Assert.Empty(secondRun);
            Assert.Equal(12, await ScalarAsync<int>(databaseConnectionString,
                "SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions;"));
            Assert.Equal(signatureBefore, await GetSchemaSignatureAsync(databaseConnectionString));

            await VerifyFailedScriptRollbackAsync(databaseConnectionString);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves a database journaled through migration 0003 upgrades exactly once.</summary>
    [Fact]
    public async Task Migration0004_UpgradesExistingPlanningDatabaseExactlyOnce()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteUpgradeTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith("0004_create_authentication_persistence.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0005_bind_sessions_to_external_identities.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0006_create_creator_memberships.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0007_create_traveler_participations.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0008_create_companion_read_role.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0009_create_adventure_plan_create_results.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0010_create_companion_policy_assignments.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0011_create_adventure_plan_template_origins.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0012_create_planner_footstep_applications.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);
            Assert.Equal(3, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions;"));

            Assert.Equal(9, (await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString)).Count);
            Assert.Equal(6, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                WHERE s.name='auth';
                """));
            Assert.Empty(await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves an existing database through 0008 receives only the idempotency migration.</summary>
    [Fact]
    public async Task Migration0009_UpgradesExistingDatabaseExactlyOnce()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteIdempotencyUpgradeTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith(
                        "0009_create_adventure_plan_create_results.sql",
                        StringComparison.Ordinal)
                    && !name.EndsWith(
                        "0010_create_companion_policy_assignments.sql",
                        StringComparison.Ordinal)
                    && !name.EndsWith(
                        "0011_create_adventure_plan_template_origins.sql",
                        StringComparison.Ordinal)
                    && !name.EndsWith(
                        "0012_create_planner_footstep_applications.sql",
                        StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);
            Assert.Equal(8, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions;"));

            IReadOnlyList<string> applied;
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                applied = DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009");

            Assert.Single(applied);
            Assert.EndsWith(
                "0009_create_adventure_plan_create_results.sql",
                applied[0],
                StringComparison.Ordinal);
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Empty(DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009"));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves exact 0009 upgrades through only 0010 with unchanged existing data.</summary>
    [Fact]
    public async Task Migration0010_UpgradesExact0009ExactlyOnce()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuitePolicyUpgrade_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(9, DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009").Count);
            await ExecuteAsync(connectionString,
                "CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;");
            var before = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0009,
                MigrationOperationalState.Classify(before.Journal));
            MigrationOperationRunner.ValidatePreMigrationState(
                before, MigrationJournalOutcome.At0009);

            IReadOnlyList<string> applied;
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                applied = DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0010");

            Assert.Single(applied);
            Assert.EndsWith("0010_create_companion_policy_assignments.sql", applied[0],
                StringComparison.Ordinal);
            var after = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0010,
                MigrationOperationalState.Classify(after.Journal));
            Assert.Equal(before.ApplicationFingerprint, after.ApplicationFingerprint);
            Assert.True(MigrationOperationRunner.VerifyExpectedPostState(after));
            Assert.Equal(MigrationOperationClassification.Complete,
                MigrationOperationRunner.ClassifyResult(
                    before, after, MigrationJournalOutcome.At0010, null));
            Assert.Equal(2, DatabaseMigratorRunner.Migrate(connectionString).Count);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves exact 0010 upgrades through only 0011 with append-only Planning authority.</summary>
    [Fact]
    public async Task Migration0011_UpgradesExact0010ExactlyOnce()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteTemplateOriginUpgrade_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(9, DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009").Count);
            await ExecuteAsync(connectionString,
                "CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;");
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Single(DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0010"));

            var before = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0010,
                MigrationOperationalState.Classify(before.Journal));
            IReadOnlyList<string> applied;
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                applied = DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0011");

            Assert.Single(applied);
            Assert.EndsWith("0011_create_adventure_plan_template_origins.sql", applied[0],
                StringComparison.Ordinal);
            Assert.Equal(MigrationJournalOutcome.At0011,
                MigrationOperationalState.Classify(
                    (await MigrationOperationalState.CaptureAsync(connectionString)).Journal));
            Assert.Equal(4, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM sys.database_permissions AS permissions
                INNER JOIN sys.database_principals AS principals
                    ON principals.principal_id=permissions.grantee_principal_id
                INNER JOIN sys.objects AS objects ON objects.object_id=permissions.major_id
                WHERE principals.name='AdventuresSuitePlanningRuntime'
                  AND objects.name='AdventurePlanTemplateOrigins'
                  AND ((permissions.state_desc='GRANT' AND permissions.permission_name IN ('SELECT','INSERT'))
                    OR (permissions.state_desc='DENY' AND permissions.permission_name IN ('UPDATE','DELETE')));
                """));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves exact 0011 upgrades through only 0012 with append-only FootStep evidence.</summary>
    [Fact]
    public async Task Migration0012_UpgradesExact0011ExactlyOnce()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteFootStepUpgrade_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(9, DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009").Count);
            await ExecuteAsync(connectionString,
                "CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;");
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(2, DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0011").Count);

            var before = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0011,
                MigrationOperationalState.Classify(before.Journal));
            IReadOnlyList<string> applied;
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                applied = DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0012");

            Assert.Single(applied);
            Assert.EndsWith("0012_create_planner_footstep_applications.sql", applied[0],
                StringComparison.Ordinal);
            Assert.Equal(MigrationJournalOutcome.At0012,
                MigrationOperationalState.Classify(
                    (await MigrationOperationalState.CaptureAsync(connectionString)).Journal));
            Assert.Equal(4, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM sys.database_permissions AS permissions
                INNER JOIN sys.database_principals AS principals
                    ON principals.principal_id=permissions.grantee_principal_id
                INNER JOIN sys.objects AS objects ON objects.object_id=permissions.major_id
                WHERE principals.name='AdventuresSuitePlanningRuntime'
                  AND objects.name='PlannerFootStepApplications'
                  AND ((permissions.state_desc='GRANT' AND permissions.permission_name IN ('SELECT','INSERT'))
                    OR (permissions.state_desc='DENY' AND permissions.permission_name IN ('UPDATE','DELETE')));
                """));
            Assert.Empty(DatabaseMigratorRunner.Migrate(connectionString));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves a concurrent migrator fails before it can inspect or mutate the journal.</summary>
    [Fact]
    public async Task MigrationLock_RejectsConcurrentMigratorAndReleasesForNextRun()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteLockTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    DatabaseMigratorRunner.Migrate(connectionString));
                Assert.Equal("Another database migration is already running.", exception.Message);
            }

            Assert.Equal(12, (await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString)).Count);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves the reviewed operation classifies each committed boundary through 0009.</summary>
    [Fact]
    public async Task ReviewedOperationState_AdvancesFrom0006To0009WithStableApplicationFingerprint()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteReviewedOperationTest_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith("0007_create_traveler_participations.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0008_create_companion_read_role.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0009_create_adventure_plan_create_results.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0010_create_companion_policy_assignments.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0011_create_adventure_plan_template_origins.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0012_create_planner_footstep_applications.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);

            using var migrationLock = DatabaseMigratorRunner.AcquireMigrationLock(connectionString);
            var before = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0006,
                MigrationOperationalState.Classify(before.Journal));

            Assert.Single(DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString, maximumMigrationNumber: "0007"));
            var at0007 = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0007,
                MigrationOperationalState.Classify(at0007.Journal));
            Assert.Equal(MigrationOperationClassification.Migration0007Committed,
                MigrationOperationRunner.ClassifyResult(
                    before, at0007, MigrationJournalOutcome.At0007, new InvalidOperationException()));

            Assert.Single(DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString, maximumMigrationNumber: "0008"));
            var at0008 = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0008,
                MigrationOperationalState.Classify(at0008.Journal));
            Assert.Equal(MigrationOperationClassification.Migration0008Committed,
                MigrationOperationRunner.ClassifyResult(
                    before, at0008, MigrationJournalOutcome.At0008, new InvalidOperationException()));

            Assert.Single(DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString, maximumMigrationNumber: "0009"));
            var after = await MigrationOperationalState.CaptureAsync(connectionString);

            Assert.Equal(MigrationJournalOutcome.At0009,
                MigrationOperationalState.Classify(after.Journal));
            Assert.Equal(before.ApplicationFingerprint, after.ApplicationFingerprint);
            Assert.True(MigrationOperationRunner.VerifyExpected0009State(after));
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Runs the real reviewed factory path from exact 0006 through only 0007-0009.</summary>
    [Fact]
    public async Task ReviewedConnectionFactoryPath_AdvancesExactlyFrom0006To0009()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteFactoryMigration_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith("0007_create_traveler_participations.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0008_create_companion_read_role.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0009_create_adventure_plan_create_results.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0010_create_companion_policy_assignments.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0011_create_adventure_plan_template_origins.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0012_create_planner_footstep_applications.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);

            var connectionCount = 0;
            SqlConnection CreateConnection()
            {
                Interlocked.Increment(ref connectionCount);
                return new SqlConnection(connectionString);
            }

            using var migrationLock = DatabaseMigratorRunner.AcquireMigrationLock(CreateConnection);
            var before = await MigrationOperationalState.CaptureAsync(CreateConnection);
            Assert.Equal(MigrationJournalOutcome.At0006,
                MigrationOperationalState.Classify(before.Journal));

            var selected = DatabaseMigratorRunner.MigrateWithLockHeld(
                CreateConnection, maximumMigrationNumber: "0009");
            var after = await MigrationOperationalState.CaptureAsync(CreateConnection);

            Assert.Equal(
                [
                    "AdventuresSuite.DatabaseMigrator.Database.Migrations.0007_create_traveler_participations.sql",
                    "AdventuresSuite.DatabaseMigrator.Database.Migrations.0008_create_companion_read_role.sql",
                    "AdventuresSuite.DatabaseMigrator.Database.Migrations.0009_create_adventure_plan_create_results.sql"
                ],
                selected);
            Assert.Equal(MigrationJournalOutcome.At0009,
                MigrationOperationalState.Classify(after.Journal));
            Assert.Equal(before.ApplicationFingerprint, after.ApplicationFingerprint);
            Assert.True(MigrationOperationRunner.VerifyExpected0009State(after));
            Assert.True(connectionCount >= 4);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Proves authentication failure occurs before state selection or script commitment.</summary>
    [Fact]
    public async Task ReviewedConnectionFactoryPath_AuthenticationFailureCommitsNoScript()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteFactoryAuthFailure_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith("0007_create_traveler_participations.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0008_create_companion_read_role.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0009_create_adventure_plan_create_results.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0010_create_companion_policy_assignments.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0011_create_adventure_plan_template_origins.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0012_create_planner_footstep_applications.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);
            var before = await MigrationOperationalState.CaptureAsync(connectionString);

            var rejected = new SqlConnectionStringBuilder(connectionString)
            {
                IntegratedSecurity = false,
                UserID = $"invalid_{Guid.NewGuid():N}",
                Password = $"Invalid-{Guid.NewGuid():N}!aA9"
            }.ConnectionString;
            Assert.Throws<SqlException>(() => DatabaseMigratorRunner.AcquireMigrationLock(
                () => new SqlConnection(rejected)));

            var after = await MigrationOperationalState.CaptureAsync(connectionString);
            Assert.Equal(MigrationJournalOutcome.At0006,
                MigrationOperationalState.Classify(after.Journal));
            Assert.Equal(before.Journal, after.Journal);
            Assert.Empty(after.RelevantObjects);
            Assert.Empty(after.CompanionPermissions);
            Assert.Empty(after.PlanningPermissions);
            Assert.Equal(before.ApplicationFingerprint, after.ApplicationFingerprint);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Runs the real DbUp pipeline with only the explicit temporary migration catalog.</summary>
    [Fact]
    public async Task RestrictedMigrationPrincipal_RunsOnly0010AndRejectsBroaderAuthority()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var suffix = Guid.NewGuid().ToString("N");
        var databaseName = $"AdventuresSuiteRestrictedMigration_{suffix}";
        var loginName = $"migration_{suffix}";
        var userName = $"migration_{suffix}";
        var password = $"Local-{Guid.NewGuid():N}!aA9";
        await ExecuteAsync(masterConnectionString,
            $"CREATE LOGIN [{loginName}] WITH PASSWORD = '{password}'; CREATE DATABASE [{databaseName}];");
        try
        {
            var administratorConnection = BuildDatabaseConnectionString(masterConnectionString, databaseName);
            await ExecuteAsync(administratorConnection, $"CREATE USER [{userName}] FOR LOGIN [{loginName}];");
            await ExecuteParameterizedAsync(administratorConnection,
                AzureDevelopmentBootstrapper.BuildMigrationGrants($"[{userName}]"), userName);
            await ExecuteParameterizedAsync(administratorConnection,
                AzureDevelopmentBootstrapper.BuildMigrationGrants($"[{userName}]"), userName);

            var assembly = typeof(MigrationCatalog).Assembly;
            var baseline = DeployChanges.To.SqlDatabase(administratorConnection)
                .WithScriptsEmbeddedInAssembly(assembly, name =>
                    MigrationCatalog.IsMigrationResource(assembly, name)
                    && !name.EndsWith("0010_create_companion_policy_assignments.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0011_create_adventure_plan_template_origins.sql", StringComparison.Ordinal)
                    && !name.EndsWith("0012_create_planner_footstep_applications.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);
            await ExecuteAsync(administratorConnection,
                "CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;");

            var restricted = new SqlConnectionStringBuilder(masterConnectionString)
            {
                InitialCatalog = databaseName,
                IntegratedSecurity = false,
                UserID = loginName,
                Password = password
            }.ConnectionString;
            async Task AssertRejectedBeforeSelectionAsync()
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    MigrationOperationRunner.VerifyPermissionsBeforeMigrationAsync(
                        () => new SqlConnection(restricted), "permission-gate-test"));
                var rejectedState = await MigrationOperationalState.CaptureAsync(administratorConnection);
                Assert.Equal(MigrationJournalOutcome.At0009,
                    MigrationOperationalState.Classify(rejectedState.Journal));
            }

            var permissions = await ReadPermissionProbeAsync(restricted);
            Assert.Equal(
                [1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0],
                permissions);
            await AssertSqlRejectedAsync(restricted,
                "UPDATE dbo.AdventuresSuiteSchemaVersions SET Applied = Applied WHERE 1 = 0;");
            await AssertSqlRejectedAsync(restricted,
                "DELETE dbo.AdventuresSuiteSchemaVersions WHERE 1 = 0;");
            await AssertSqlRejectedAsync(restricted, "CREATE SCHEMA prohibited;");
            await AssertSqlRejectedAsync(restricted, "CREATE ROLE prohibited;");
            await AssertSqlRejectedAsync(restricted, "CREATE TABLE dbo.Prohibited (Id int);");

            await ExecuteAsync(administratorConnection, $"GRANT ALTER ANY ROLE TO [{userName}];");
            await AssertRejectedBeforeSelectionAsync();
            await ExecuteAsync(administratorConnection, $"REVOKE ALTER ANY ROLE FROM [{userName}];");

            await ExecuteAsync(administratorConnection,
                $"REVOKE SELECT ON dbo.AdventuresSuiteSchemaVersions FROM [{userName}];");
            await AssertRejectedBeforeSelectionAsync();
            await ExecuteAsync(administratorConnection,
                $"GRANT SELECT ON dbo.AdventuresSuiteSchemaVersions TO [{userName}];");

            await ExecuteAsync(administratorConnection,
                $"CREATE ROLE [unexpected_{suffix}]; ALTER ROLE [unexpected_{suffix}] ADD MEMBER [{userName}];");
            await AssertRejectedBeforeSelectionAsync();
            await ExecuteAsync(administratorConnection,
                $"ALTER ROLE [unexpected_{suffix}] DROP MEMBER [{userName}]; DROP ROLE [unexpected_{suffix}];");

            await ExecuteAsync(administratorConnection,
                $"GRANT CONTROL ON SCHEMA::dbo TO [{userName}];");
            await AssertRejectedBeforeSelectionAsync();
            await ExecuteAsync(administratorConnection,
                $"REVOKE CONTROL ON SCHEMA::dbo FROM [{userName}];");

            await ExecuteAsync(administratorConnection,
                $"ALTER ROLE db_datareader ADD MEMBER [{userName}];");
            await AssertRejectedBeforeSelectionAsync();
            await ExecuteAsync(administratorConnection,
                $"ALTER ROLE db_datareader DROP MEMBER [{userName}];");

            using (DatabaseMigratorRunner.AcquireMigrationLock(restricted))
            {
                var before = await MigrationOperationalState.CaptureAsync(restricted);
                Assert.Equal(MigrationJournalOutcome.At0009,
                    MigrationOperationalState.Classify(before.Journal));
                MigrationOperationRunner.ValidatePreMigrationState(
                    before, MigrationJournalOutcome.At0009);
                await MigrationOperationRunner.VerifyPermissionsBeforeMigrationAsync(
                    () => new SqlConnection(restricted), "permission-gate-test");
                Assert.Single(DatabaseMigratorRunner.MigrateWithLockHeld(
                    restricted, maximumMigrationNumber: "0010"));
                var after = await MigrationOperationalState.CaptureAsync(restricted);
                Assert.Equal(MigrationJournalOutcome.At0010,
                    MigrationOperationalState.Classify(after.Journal));
                Assert.Equal(before.ApplicationFingerprint, after.ApplicationFingerprint);
                Assert.True(MigrationOperationRunner.VerifyExpectedPostState(after));

                await ExecuteAsync(administratorConnection, ValidSecondCreatorPlanSql);
                var changedApplicationData = await MigrationOperationalState.CaptureAsync(restricted);
                Assert.NotEqual(before.ApplicationFingerprint,
                    changedApplicationData.ApplicationFingerprint);
                Assert.Equal(MigrationOperationClassification.Unexpected,
                    MigrationOperationRunner.ClassifyResult(
                        before, changedApplicationData, MigrationJournalOutcome.At0010, null));
            }

            using (DatabaseMigratorRunner.AcquireMigrationLock(restricted))
                Assert.Empty(DatabaseMigratorRunner.MigrateWithLockHeld(
                    restricted, maximumMigrationNumber: "0010"));
            await AzureDevelopmentBootstrapper.VerifyMigrationPermissionsAsync(restricted);
            var state = await MigrationOperationalState.CaptureAsync(restricted);
            Assert.Equal(MigrationJournalOutcome.At0010, MigrationOperationalState.Classify(state.Journal));
            Assert.True(MigrationOperationRunner.VerifyExpectedPostState(state));

            using (DatabaseMigratorRunner.AcquireMigrationLock(restricted))
            {
                Assert.Throws<InvalidOperationException>(() => DatabaseMigratorRunner.Migrate(restricted));
            }
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
            await ExecuteAsync(masterConnectionString,
                $"IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'{loginName}') DROP LOGIN [{loginName}];");
        }
    }

    /// <summary>Proves malformed bootstrapped runtime roles stop at the production pre-state gate.</summary>
    [Fact]
    public async Task Exact0009PolicyRoleGate_RejectsMalformedRoleBeforeDbUpSelection()
    {
        var masterConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(masterConnectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        var databaseName = $"AdventuresSuiteBootstrappedGate_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(9, DatabaseMigratorRunner.MigrateWithLockHeld(
                    connectionString, maximumMigrationNumber: "0009").Count);
            await ExecuteAsync(connectionString,
                "CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;");

            async Task AssertGateAcceptsAsync()
            {
                var state = await MigrationOperationalState.CaptureAsync(connectionString);
                Assert.Equal(MigrationJournalOutcome.At0009,
                    MigrationOperationalState.Classify(state.Journal));
                MigrationOperationRunner.ValidatePreMigrationState(
                    state, MigrationJournalOutcome.At0009);
            }

            async Task AssertGateRejectsAsync()
            {
                var state = await MigrationOperationalState.CaptureAsync(connectionString);
                Assert.Equal(MigrationJournalOutcome.At0009,
                    MigrationOperationalState.Classify(state.Journal));
                Assert.Throws<InvalidOperationException>(() =>
                    MigrationOperationRunner.ValidatePreMigrationState(
                        state, MigrationJournalOutcome.At0009));
                Assert.Equal(9, state.Journal.Count);
            }

            await AssertGateAcceptsAsync();

            await ExecuteAsync(connectionString, """
                DROP ROLE AdventuresSuiteCompanionPolicyRuntime;
                CREATE ROLE AdventuresSuiteCompanionPolicyRuntimeSubstitute AUTHORIZATION dbo;
                """);
            await AssertGateRejectsAsync();
            await ExecuteAsync(connectionString, """
                DROP ROLE AdventuresSuiteCompanionPolicyRuntimeSubstitute;
                CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;
                """);

            await ExecuteAsync(connectionString, """
                CREATE USER malformed_role_owner WITHOUT LOGIN;
                ALTER AUTHORIZATION ON ROLE::AdventuresSuiteCompanionPolicyRuntime TO malformed_role_owner;
                """);
            await AssertGateRejectsAsync();
            await ExecuteAsync(connectionString, """
                ALTER AUTHORIZATION ON ROLE::AdventuresSuiteCompanionPolicyRuntime TO dbo;
                DROP USER malformed_role_owner;
                """);

            await ExecuteAsync(connectionString, """
                CREATE ROLE malformed_parent_role AUTHORIZATION dbo;
                ALTER ROLE malformed_parent_role ADD MEMBER AdventuresSuiteCompanionPolicyRuntime;
                """);
            await AssertGateRejectsAsync();
            await ExecuteAsync(connectionString, """
                ALTER ROLE malformed_parent_role DROP MEMBER AdventuresSuiteCompanionPolicyRuntime;
                DROP ROLE malformed_parent_role;
                """);

            await ExecuteAsync(connectionString, """
                CREATE USER malformed_role_member WITHOUT LOGIN;
                ALTER ROLE AdventuresSuiteCompanionPolicyRuntime ADD MEMBER malformed_role_member;
                """);
            await AssertGateRejectsAsync();
            await ExecuteAsync(connectionString, """
                ALTER ROLE AdventuresSuiteCompanionPolicyRuntime DROP MEMBER malformed_role_member;
                DROP USER malformed_role_member;
                """);

            await ExecuteAsync(connectionString, """
                GRANT SELECT ON OBJECT::planning.AdventurePlans TO AdventuresSuiteCompanionPolicyRuntime;
                """);
            await AssertGateRejectsAsync();
            await ExecuteAsync(connectionString, """
                REVOKE SELECT ON OBJECT::planning.AdventurePlans FROM AdventuresSuiteCompanionPolicyRuntime;
                """);

            await AssertGateAcceptsAsync();
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    private static async Task<IReadOnlyList<int>> ReadPermissionProbeAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Value FROM (VALUES
              (1, HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONNECT')),
              (2, HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE')),
              (3, HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION')),
              (4, HAS_PERMS_BY_NAME('planning','SCHEMA','CONTROL')),
              (5, HAS_PERMS_BY_NAME('auth','SCHEMA','CONTROL')),
              (6, HAS_PERMS_BY_NAME('audit','SCHEMA','CONTROL')),
              (7, HAS_PERMS_BY_NAME('dbo.AdventuresSuiteSchemaVersions','OBJECT','SELECT')),
              (8, HAS_PERMS_BY_NAME('dbo.AdventuresSuiteSchemaVersions','OBJECT','INSERT')),
              (9, HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER ANY ROLE')),
              (10, HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE SCHEMA')),
              (11, HAS_PERMS_BY_NAME('dbo','SCHEMA','CONTROL')),
              (12, HAS_PERMS_BY_NAME('dbo.AdventuresSuiteSchemaVersions','OBJECT','UPDATE')),
              (13, HAS_PERMS_BY_NAME('dbo.AdventuresSuiteSchemaVersions','OBJECT','DELETE')),
              (14, ISNULL(IS_ROLEMEMBER('db_owner'),0)),
              (15, ISNULL(IS_ROLEMEMBER('db_ddladmin'),0)),
              (16, ISNULL(IS_ROLEMEMBER('db_datareader'),0))) AS Permissions(Sequence, Value)
            ORDER BY Sequence;
            """;
        var values = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) values.Add(reader.GetInt32(0));
        return values;
    }

    private static async Task ExecuteParameterizedAsync(
        string connectionString, string sql, string alias)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@AliasParameter", alias);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task VerifySchemaAsync(string connectionString)
    {
        Assert.Equal(3, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*)
            FROM sys.schemas AS schemas
            INNER JOIN sys.database_principals AS principals
                ON principals.principal_id = schemas.principal_id
            WHERE schemas.name IN ('planning', 'auth', 'audit')
              AND principals.name = 'db_ddladmin';
            """));

        const string childTableSql = """
            SELECT COUNT(*)
            FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'planning'
              AND tables.name <> 'AdventurePlans';
            """;
        Assert.Equal(17, await ScalarAsync<int>(connectionString, childTableSql));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'dbo' AND tables.name = 'AdventuresSuiteSchemaVersions';
            """));

        Assert.Equal(3, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'auth'
              AND tables.name IN ('Users', 'ExternalIdentities', 'UserSessions');
            """));
        Assert.Equal(3, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'auth'
              AND tables.name IN ('CreatorMemberships', 'CreatorMembershipRoles',
                                  'CreatorMembershipPermissionGrants');
            """));
        Assert.Equal(2, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'audit'
              AND tables.name IN ('AuditEvents', 'CompanionInformationPolicyAssignmentEvents');
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.columns AS columns
            INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'auth' AND tables.name = 'UserSessions'
              AND columns.name = 'ExternalIdentityId'
              AND columns.collation_name = 'Latin1_General_100_BIN2';
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE name = 'FK_UserSessions_ExternalIdentity';
            """));
        Assert.Equal(3, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.columns AS columns
            INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'auth'
              AND ((tables.name = 'ExternalIdentities' AND columns.name IN ('Issuer', 'Subject'))
                   OR (tables.name = 'Users' AND columns.name = 'UserId'))
              AND columns.collation_name = 'Latin1_General_100_BIN2';
            """));

        var creatorColumns = await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*)
            FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'planning'
              AND EXISTS
              (
                  SELECT 1 FROM sys.columns AS columns
                  WHERE columns.object_id = tables.object_id AND columns.name = 'CreatorId'
              );
            """);
        Assert.Equal(18, creatorColumns);

        Assert.True(await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE 'FK[_]%';") >= 12);
        Assert.True(await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name LIKE 'CK[_]%';") >= 15);
        Assert.True(await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'planning' AND indexes.name IS NOT NULL;
            """) >= 16);
    }

    private static async Task VerifyConstraintsAsync(string connectionString)
    {
        await ExecuteAsync(connectionString, ValidGraphSql);

        await ExecuteAsync(connectionString, ValidSecondCreatorPlanSql);
        Assert.Equal(2, await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM planning.AdventurePlans WHERE AdventurePlanId = 'plan_shared';"));

        await AssertSqlRejectedAsync(connectionString, CrossCreatorForeignKeySql);
        await AssertSqlRejectedAsync(connectionString, InvalidStatusSql);
        await AssertSqlRejectedAsync(connectionString, ReversedDatesSql);
        await AssertSqlRejectedAsync(connectionString, NonpositiveVersionSql);
        await AssertSqlRejectedAsync(connectionString, DuplicateSequenceSql);
        await AssertSqlRejectedAsync(connectionString, NegativeCurrencySql);
        await AssertSqlRejectedAsync(connectionString, InvalidCurrencySql);
    }

    private static async Task VerifyFailedScriptRollbackAsync(string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScript("9999_failed_rollback_probe.sql", """
                CREATE TABLE planning.RollbackProbe (Id int NOT NULL);
                THROW 51000, 'Expected migration rollback probe.', 1;
                """)
            .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
            .WithTransactionPerScript()
            .Build();

        var result = upgrader.PerformUpgrade();

        Assert.False(result.Successful);
        Assert.Equal(0, await ScalarAsync<int>(connectionString,
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('planning.RollbackProbe');"));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions
            WHERE ScriptName = '9999_failed_rollback_probe.sql';
            """));
    }

    private static async Task VerifyRuntimePermissionsAsync(string connectionString)
    {
        await ExecuteAsync(connectionString, """
            CREATE USER planning_runtime_test WITHOUT LOGIN;
            ALTER ROLE AdventuresSuitePlanningRuntime ADD MEMBER planning_runtime_test;
            """);
        foreach (var objectName in new[]
        {
            "planning.AdventurePlanCreateResults",
            "planning.AdventurePlanTemplateOrigins",
            "planning.PlannerFootStepApplications"
        })
        {
            foreach (var permission in new[] { "SELECT", "INSERT" })
            {
                Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
                    EXECUTE AS USER='planning_runtime_test';
                    DECLARE @Allowed int = HAS_PERMS_BY_NAME(
                        '{objectName}', 'OBJECT', '{permission}');
                    REVERT;
                    SELECT @Allowed;
                    """));
            }
            foreach (var permission in new[] { "UPDATE", "DELETE" })
            {
                Assert.Equal(0, await ScalarAsync<int>(connectionString, $"""
                    EXECUTE AS USER='planning_runtime_test';
                    DECLARE @Denied int = HAS_PERMS_BY_NAME(
                        '{objectName}', 'OBJECT', '{permission}');
                    REVERT;
                    SELECT @Denied;
                    """));
            }
        }

        await ExecuteAsync(connectionString, """
            CREATE USER authentication_runtime_test WITHOUT LOGIN;
            ALTER ROLE AdventuresSuiteAuthenticationRuntime ADD MEMBER authentication_runtime_test;
            EXECUTE AS USER='authentication_runtime_test';
            INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc)
            VALUES ('user_runtime_probe','Active',1,'2026-08-08T15:00:00+00:00','2026-08-08T15:00:00+00:00');
            SELECT COUNT(*) FROM auth.Users WHERE UserId='user_runtime_probe';
            REVERT;
            """);
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='authentication_runtime_test';
            DECLARE @HasAlter int = HAS_PERMS_BY_NAME('auth', 'SCHEMA', 'ALTER');
            REVERT;
            SELECT @HasAlter;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='authentication_runtime_test';
            DECLARE @HasDelete int = HAS_PERMS_BY_NAME('auth.Users', 'OBJECT', 'DELETE');
            REVERT;
            SELECT @HasDelete;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='authentication_runtime_test';
            DECLARE @CanWriteJournal int = HAS_PERMS_BY_NAME(
                'dbo.AdventuresSuiteSchemaVersions', 'OBJECT', 'INSERT');
            REVERT;
            SELECT @CanWriteJournal;
            """));
        await ExecuteAsync(connectionString, """
            CREATE USER membership_runtime_test WITHOUT LOGIN;
            ALTER ROLE AdventuresSuiteMembershipRuntime ADD MEMBER membership_runtime_test;
            """);
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='membership_runtime_test';
            DECLARE @CanInsert int = HAS_PERMS_BY_NAME('audit.AuditEvents', 'OBJECT', 'INSERT');
            REVERT;
            SELECT @CanInsert;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='membership_runtime_test';
            DECLARE @CanUpdate int = HAS_PERMS_BY_NAME('audit.AuditEvents', 'OBJECT', 'UPDATE');
            REVERT;
            SELECT @CanUpdate;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='membership_runtime_test';
            DECLARE @CanDelete int = HAS_PERMS_BY_NAME('audit.AuditEvents', 'OBJECT', 'DELETE');
            REVERT;
            SELECT @CanDelete;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='membership_runtime_test';
            DECLARE @CanDelete int = HAS_PERMS_BY_NAME('auth.CreatorMemberships', 'OBJECT', 'DELETE');
            REVERT;
            SELECT @CanDelete;
            """));

        await ExecuteAsync(connectionString, """
            CREATE USER companion_read_runtime_test WITHOUT LOGIN;
            CREATE USER companion_privilege_target WITHOUT LOGIN;
            ALTER ROLE AdventuresSuiteCompanionReadRuntime ADD MEMBER companion_read_runtime_test;
            """);
        await ExecuteAsync(connectionString,
            "CREATE PROCEDURE dbo.CompanionDeniedExecutionProbe AS SELECT 1;");
        foreach (var target in new[]
        {
            "planning.AdventurePlans",
            "planning.TravelerParticipations",
            "planning.DestinationVisits",
            "auth.CreatorMemberships",
            "auth.CreatorMembershipRoles",
            "auth.CreatorMembershipPermissionGrants",
            "planning.CompanionInformationPolicyAssignments"
        })
        {
            Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
                EXECUTE AS USER='companion_read_runtime_test';
                DECLARE @Allowed int = HAS_PERMS_BY_NAME('{target}', 'OBJECT', 'SELECT');
                REVERT;
                SELECT @Allowed;
                """));
            await ExecuteAsync(connectionString, $"""
                EXECUTE AS USER='companion_read_runtime_test';
                SELECT TOP (0) * FROM {target};
                REVERT;
                """);
            foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE", "ALTER", "CONTROL" })
            {
                Assert.Equal(0, await ScalarAsync<int>(connectionString, $"""
                    EXECUTE AS USER='companion_read_runtime_test';
                    DECLARE @Denied int = HAS_PERMS_BY_NAME('{target}', 'OBJECT', '{permission}');
                    REVERT;
                    SELECT @Denied;
                    """));
            }
        }
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='companion_read_runtime_test';
            DECLARE @BroadRead int = IS_ROLEMEMBER('db_datareader');
            REVERT;
            SELECT @BroadRead;
            """));
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='companion_read_runtime_test';
            DECLARE @CanDdl int = IS_ROLEMEMBER('db_ddladmin');
            REVERT;
            SELECT @CanDdl;
            """));

        await ExecuteAsync(connectionString, """
            CREATE USER companion_policy_runtime_test WITHOUT LOGIN;
            ALTER ROLE AdventuresSuiteCompanionPolicyRuntime ADD MEMBER companion_policy_runtime_test;
            """);
        foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE" })
        {
            Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""
                EXECUTE AS USER='companion_policy_runtime_test';
                DECLARE @Allowed int = HAS_PERMS_BY_NAME(
                    'planning.CompanionInformationPolicyAssignments', 'OBJECT', '{permission}');
                REVERT;
                SELECT @Allowed;
                """));
        }
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='companion_policy_runtime_test';
            DECLARE @Denied int = HAS_PERMS_BY_NAME(
                'planning.CompanionInformationPolicyAssignments', 'OBJECT', 'DELETE');
            REVERT;
            SELECT @Denied;
            """));
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            EXECUTE AS USER='companion_policy_runtime_test';
            DECLARE @Allowed int = HAS_PERMS_BY_NAME(
                'audit.CompanionInformationPolicyAssignmentEvents', 'OBJECT', 'INSERT');
            REVERT;
            SELECT @Allowed;
            """));
        foreach (var permission in new[] { "UPDATE", "DELETE" })
        {
            Assert.Equal(0, await ScalarAsync<int>(connectionString, $"""
                EXECUTE AS USER='companion_policy_runtime_test';
                DECLARE @Denied int = HAS_PERMS_BY_NAME(
                    'audit.CompanionInformationPolicyAssignmentEvents', 'OBJECT', '{permission}');
                REVERT;
                SELECT @Denied;
                """));
        }

        foreach (var (operationName, sql) in new[]
        {
            ("unapproved read", "SELECT TOP (0) * FROM auth.Users;"),
            ("insert", "INSERT planning.AdventurePlans (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus, Version, CreatedAtUtc, UpdatedAtUtc) VALUES ('denied', 'denied', 'denied', 'Dream', 'Draft', 1, SYSUTCDATETIME(), SYSUTCDATETIME());"),
            ("update", "UPDATE planning.AdventurePlans SET Title = Title WHERE 1 = 0;"),
            ("delete", "DELETE FROM planning.AdventurePlans WHERE 1 = 0;"),
            ("execute", "EXECUTE dbo.CompanionDeniedExecutionProbe;"),
            ("DDL", "CREATE TABLE planning.CompanionDeniedDdlProbe (Id int NOT NULL);"),
            ("migration journal write", "INSERT dbo.AdventuresSuiteSchemaVersions (ScriptName, Applied) VALUES ('denied', SYSUTCDATETIME());")
        })
        {
            var exception = await Record.ExceptionAsync(() => ExecuteAsync(connectionString, $"""
                EXECUTE AS USER='companion_read_runtime_test';
                {sql}
                REVERT;
                """));
            Assert.True(exception is SqlException,
                $"Prohibited Companion operation '{operationName}' unexpectedly succeeded.");
        }

        var escalation = await DiagnosePrivilegeEscalationAsync(connectionString);
        Assert.Equal(0, escalation.ControlBefore);
        Assert.Equal(0, escalation.ControlAfter);
        Assert.Equal(0, escalation.TargetSelectAfter);
        Assert.Equal(escalation.SelfControlGrantRows, escalation.SelfGrantedByRuntimeRows);
        Assert.Equal(0, escalation.TargetGrantRows);
        Assert.Equal(0, escalation.DelegationAccepted);
        Assert.Equal("PermissionDenied", escalation.DelegationErrorCategory);
        Assert.True(escalation.DelegationErrorNumber > 0);
        Assert.Equal(1, escalation.ImpersonationCleared);
        Assert.Equal(1, escalation.TransactionCleared);
        Assert.Equal("dbo", escalation.RoleOwner);
        Assert.Equal(0, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.database_permissions
            WHERE (grantee_principal_id = USER_ID(N'companion_read_runtime_test')
                   AND permission_name = N'CONTROL'
                   AND major_id = OBJECT_ID(N'planning.AdventurePlans'))
               OR (grantee_principal_id = USER_ID(N'companion_privilege_target')
                   AND permission_name = N'SELECT'
                   AND major_id = OBJECT_ID(N'planning.AdventurePlans'));
            """));

        const string operationalObjectId = "11111111-2222-3333-4444-555555555555";
        const string operationalClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var operationalAlias = AzureDevelopmentBootstrapper.CreatePrincipalAlias(
            "companion-api-verifier", Guid.Parse(operationalObjectId));
        await ExecuteAsync(connectionString, $"""
            CREATE USER [{operationalAlias}] WITHOUT LOGIN;
            ALTER ROLE AdventuresSuiteCompanionReadRuntime ADD MEMBER [{operationalAlias}];
            """);
        await AzureDevelopmentBootstrapper.VerifyCompanionReadPermissionsAsync(
            connectionString,
            operationalObjectId,
            operationalClientId,
            "companion-api-verifier");
    }

    private static async Task<PrivilegeEscalationDiagnostic> DiagnosePrivilegeEscalationAsync(
        string connectionString)
    {
        const string sql = """
            SET XACT_ABORT OFF;
            DECLARE @ControlBefore int;
            DECLARE @ControlAfter int;
            DECLARE @TargetSelectAfter int;
            DECLARE @SelfControlGrantRows int;
            DECLARE @SelfGrantedByRuntimeRows int;
            DECLARE @TargetGrantRows int;
            DECLARE @RoleOwner sysname;
            DECLARE @AdminUser sysname = USER_NAME();
            DECLARE @Impersonating bit = 0;
            DECLARE @ImpersonationCleared int = 0;
            DECLARE @TransactionCleared int = 0;
            DECLARE @DelegationAccepted int = 0;
            DECLARE @DelegationErrorNumber int = 0;
            DECLARE @DelegationErrorCategory nvarchar(32) = N'None';

            BEGIN TRY
                BEGIN TRANSACTION;
                EXECUTE AS USER = N'companion_read_runtime_test';
                SET @Impersonating = 1;
                SET @ControlBefore = HAS_PERMS_BY_NAME(
                    N'planning.AdventurePlans', N'OBJECT', N'CONTROL');
                GRANT CONTROL ON OBJECT::planning.AdventurePlans TO companion_read_runtime_test;
                SET @ControlAfter = HAS_PERMS_BY_NAME(
                    N'planning.AdventurePlans', N'OBJECT', N'CONTROL');
                BEGIN TRY
                    GRANT SELECT ON OBJECT::planning.AdventurePlans TO companion_privilege_target;
                    SET @DelegationAccepted = 1;
                END TRY
                BEGIN CATCH
                    SET @DelegationErrorNumber = ERROR_NUMBER();
                    SET @DelegationErrorCategory = CASE
                        WHEN ERROR_NUMBER() IN (229, 15151) THEN N'PermissionDenied'
                        ELSE N'Other'
                    END;
                END CATCH;
                REVERT;
                SET @Impersonating = 0;

                EXECUTE AS USER = N'companion_privilege_target';
                SET @Impersonating = 1;
                SET @TargetSelectAfter = HAS_PERMS_BY_NAME(
                    N'planning.AdventurePlans', N'OBJECT', N'SELECT');
                REVERT;
                SET @Impersonating = 0;

                SELECT @SelfControlGrantRows = COUNT(*) FROM sys.database_permissions
                WHERE grantee_principal_id = USER_ID(N'companion_read_runtime_test')
                  AND permission_name = N'CONTROL'
                  AND major_id = OBJECT_ID(N'planning.AdventurePlans');
                SELECT @SelfGrantedByRuntimeRows = COUNT(*) FROM sys.database_permissions
                WHERE grantee_principal_id = USER_ID(N'companion_read_runtime_test')
                  AND grantor_principal_id = USER_ID(N'companion_read_runtime_test')
                  AND permission_name = N'CONTROL'
                  AND major_id = OBJECT_ID(N'planning.AdventurePlans');
                SELECT @TargetGrantRows = COUNT(*) FROM sys.database_permissions
                WHERE grantee_principal_id = USER_ID(N'companion_privilege_target')
                  AND permission_name = N'SELECT'
                  AND major_id = OBJECT_ID(N'planning.AdventurePlans');
                SELECT @RoleOwner = owner.name
                FROM sys.database_principals AS role
                INNER JOIN sys.database_principals AS owner
                    ON owner.principal_id = role.owning_principal_id
                WHERE role.name = N'AdventuresSuiteCompanionReadRuntime';
            END TRY
            BEGIN CATCH
                IF @Impersonating = 1
                BEGIN
                    REVERT;
                    SET @Impersonating = 0;
                END;
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            SET @ImpersonationCleared = CASE WHEN USER_NAME() = @AdminUser THEN 1 ELSE 0 END;
            SET @TransactionCleared = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;
            SELECT @ControlBefore, @ControlAfter, @TargetSelectAfter,
                   @SelfControlGrantRows, @SelfGrantedByRuntimeRows,
                   @TargetGrantRows, @RoleOwner, @DelegationAccepted,
                   @DelegationErrorNumber, @DelegationErrorCategory,
                   @ImpersonationCleared, @TransactionCleared;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
            reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6),
            reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9),
            reader.GetInt32(10), reader.GetInt32(11));
    }

    private sealed record PrivilegeEscalationDiagnostic(
        int ControlBefore,
        int ControlAfter,
        int TargetSelectAfter,
        int SelfControlGrantRows,
        int SelfGrantedByRuntimeRows,
        int TargetGrantRows,
        string RoleOwner,
        int DelegationAccepted,
        int DelegationErrorNumber,
        string DelegationErrorCategory,
        int ImpersonationCleared,
        int TransactionCleared);

    private static async Task<string> GetSchemaSignatureAsync(string connectionString)
    {
        const string sql = """
            SELECT CONCAT('T|', schemas.name, '|', tables.name, '|', columns.column_id,
                          '|', columns.name, '|', types.name, '|', columns.max_length,
                          '|', columns.is_nullable) COLLATE DATABASE_DEFAULT
            FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            INNER JOIN sys.columns AS columns ON columns.object_id = tables.object_id
            INNER JOIN sys.types AS types ON types.user_type_id = columns.user_type_id
            WHERE schemas.name IN ('planning', 'auth', 'audit', 'dbo')
              AND (schemas.name IN ('planning', 'auth', 'audit') OR tables.name = 'AdventuresSuiteSchemaVersions')
            UNION ALL
            SELECT CONCAT('O|', schemas.name, '|', objects.name, '|', objects.type, '|', objects.object_id)
                COLLATE DATABASE_DEFAULT
            FROM sys.objects AS objects
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = objects.schema_id
            WHERE schemas.name IN ('planning', 'auth', 'audit')
              AND objects.type IN ('PK', 'F', 'C', 'UQ')
            ORDER BY 1;
            """;
        var rows = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    private static async Task AssertSqlRejectedAsync(string connectionString, string sql)
    {
        await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connectionString, sql));
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
        {
            throw new InvalidOperationException("The SQL scalar query returned no value.");
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateDatabaseAsync(string masterConnectionString, string databaseName) =>
        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{databaseName}];");

    private static async Task DropDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await ExecuteAsync(masterConnectionString, $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            """);
    }

    private static string BuildDatabaseConnectionString(
        string masterConnectionString,
        string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private const string ValidGraphSql = """
        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus,
             StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_shared', 'Valid plan', 'Plan', 'Draft',
             '2027-10-25', '2027-11-15', 1, '2026-08-07T20:00:00+00:00', '2026-08-07T20:00:00+00:00');
        INSERT planning.DestinationVisits
            (CreatorId, AdventurePlanId, DestinationVisitId, Name, StartDate, EndDate, TimeZone, Sequence)
        VALUES
            ('creator_alpha', 'plan_shared', 'visit_madrid', 'Madrid',
             '2027-10-26', '2027-10-29', 'Europe/Madrid', 1);
        INSERT planning.ItineraryDays
            (CreatorId, AdventurePlanId, ItineraryDayId, DestinationVisitId, LocalDate, TimeZone, Title)
        VALUES
            ('creator_alpha', 'plan_shared', 'day_madrid', 'visit_madrid',
             '2027-10-26', 'Europe/Madrid', 'Madrid');
        INSERT planning.BudgetItems
            (CreatorId, AdventurePlanId, BudgetItemId, Description, Amount, CurrencyCode)
        VALUES
            ('creator_alpha', 'plan_shared', 'budget_cruise', 'Cruise', 5000.00, 'USD');
        """;

    private const string ValidSecondCreatorPlanSql = """
        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus,
             StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_beta', 'plan_shared', 'Same plan identity', 'Plan', 'Draft',
             '2027-10-25', '2027-11-15', 1, '2026-08-07T20:00:00+00:00', '2026-08-07T20:00:00+00:00');
        """;

    private const string CrossCreatorForeignKeySql = """
        INSERT planning.DestinationVisits
            (CreatorId, AdventurePlanId, DestinationVisitId, Name, StartDate, EndDate, TimeZone, Sequence)
        VALUES
            ('creator_gamma', 'plan_shared', 'visit_invalid', 'Invalid',
             '2027-10-26', '2027-10-27', 'Europe/Madrid', 1);
        """;

    private const string InvalidStatusSql = """
        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus,
             StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_bad_status', 'Invalid', 'Plan', 'Unknown',
             '2027-10-25', '2027-11-15', 1, '2026-08-07T20:00:00+00:00', '2026-08-07T20:00:00+00:00');
        """;

    private const string ReversedDatesSql = """
        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus,
             StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_bad_dates', 'Invalid', 'Plan', 'Draft',
             '2027-11-15', '2027-10-25', 1, '2026-08-07T20:00:00+00:00', '2026-08-07T20:00:00+00:00');
        """;

    private const string NonpositiveVersionSql = """
        INSERT planning.AdventurePlans
            (CreatorId, AdventurePlanId, Title, LifecycleStage, PlanningStatus,
             StartDate, EndDate, Version, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            ('creator_alpha', 'plan_bad_version', 'Invalid', 'Plan', 'Draft',
             '2027-10-25', '2027-11-15', 0, '2026-08-07T20:00:00+00:00', '2026-08-07T20:00:00+00:00');
        """;

    private const string DuplicateSequenceSql = """
        INSERT planning.DestinationVisits
            (CreatorId, AdventurePlanId, DestinationVisitId, Name, StartDate, EndDate, TimeZone, Sequence)
        VALUES
            ('creator_alpha', 'plan_shared', 'visit_barcelona', 'Barcelona',
             '2027-10-30', '2027-11-01', 'Europe/Madrid', 1);
        """;

    private const string NegativeCurrencySql = """
        INSERT planning.BudgetItems
            (CreatorId, AdventurePlanId, BudgetItemId, Description, Amount, CurrencyCode)
        VALUES
            ('creator_alpha', 'plan_shared', 'budget_negative', 'Invalid', -1.00, 'USD');
        """;

    private const string InvalidCurrencySql = """
        INSERT planning.BudgetItems
            (CreatorId, AdventurePlanId, BudgetItemId, Description, Amount, CurrencyCode)
        VALUES
            ('creator_alpha', 'plan_shared', 'budget_currency', 'Invalid', 1.00, 'usd');
        """;
}
