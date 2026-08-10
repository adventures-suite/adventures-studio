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
            var firstRun = DatabaseMigratorRunner.Migrate(databaseConnectionString);
            Assert.Equal(7, firstRun.Count);

            await VerifySchemaAsync(databaseConnectionString);
            await VerifyConstraintsAsync(databaseConnectionString);
            await VerifyRuntimePermissionsAsync(databaseConnectionString);
            var signatureBefore = await GetSchemaSignatureAsync(databaseConnectionString);

            var secondRun = DatabaseMigratorRunner.Migrate(databaseConnectionString);

            Assert.Empty(secondRun);
            Assert.Equal(7, await ScalarAsync<int>(databaseConnectionString,
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
                    && !name.EndsWith("0007_create_traveler_participations.sql", StringComparison.Ordinal))
                .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
                .WithTransactionPerScript()
                .Build()
                .PerformUpgrade();
            Assert.True(baseline.Successful, baseline.Error?.Message);
            Assert.Equal(3, await ScalarAsync<int>(connectionString,
                "SELECT COUNT(*) FROM dbo.AdventuresSuiteSchemaVersions;"));

            Assert.Equal(4, DatabaseMigratorRunner.Migrate(connectionString).Count);
            Assert.Equal(6, await ScalarAsync<int>(connectionString, """
                SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                WHERE s.name='auth';
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

            Assert.Equal(7, DatabaseMigratorRunner.Migrate(connectionString).Count);
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
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
        Assert.Equal(13, await ScalarAsync<int>(connectionString, childTableSql));
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
        Assert.Equal(1, await ScalarAsync<int>(connectionString, """
            SELECT COUNT(*) FROM sys.tables AS tables
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = 'audit' AND tables.name = 'AuditEvents';
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
        Assert.Equal(14, creatorColumns);

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
    }

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
