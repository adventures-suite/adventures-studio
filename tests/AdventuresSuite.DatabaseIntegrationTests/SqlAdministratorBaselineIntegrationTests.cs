using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Executes the complete reviewed administrator baseline through its production reader.</summary>
public sealed class SqlAdministratorBaselineIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private const string MigrationPrincipalName = "AdventuresSuiteMigrationDev-ffc9a";

    /// <summary>Consumes every baseline result set for an absent database state.</summary>
    [Fact]
    public async Task BaselineReader_ConsumesCompleteQueryForAbsentState()
    {
        var masterConnectionString = RequireConnectionString();
        var databaseName = $"AdventuresSuiteAdminBaselineAbsent_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
        await CreateDatabaseAsync(masterConnectionString, databaseName);

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var result = await CaptureBaselineAsync(connection, databaseName);
            Assert.True(result.ExitCode == 0, result.Evidence.RootElement.ToString());
            Assert.Equal("absent", result.Evidence.RootElement.GetProperty("outcome").GetString());
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
        }
    }

    /// <summary>Accepts only the exact DbUp-qualified canonical 0001-0006 state.</summary>
    [Fact]
    public async Task BaselineReader_AcceptsExactQualifiedAt0006State()
    {
        var master = RequireConnectionString();
        var databaseName = $"AdventuresSuiteAdminBaseline0006_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await CreateDatabaseAsync(master, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                Assert.Equal(6, DatabaseMigratorRunner.MigrateWithLockHeld(connectionString, "0006").Count);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var result = await CaptureBaselineAsync(connection, databaseName);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("At0006", result.Evidence.RootElement.GetProperty("outcome").GetString());
            Assert.All(result.Evidence.RootElement.GetProperty("journal").GetProperty("scripts").EnumerateArray(),
                script => Assert.DoesNotContain("AdventuresSuite.DatabaseMigrator.Database.Migrations.", script.GetString()));
        }
        finally { await DropDatabaseAsync(master, databaseName); }
    }

    /// <summary>Rejects noncanonical journals and other partial committed boundaries.</summary>
    [Theory]
    [InlineData("0005", null)]
    [InlineData("0007", null)]
    [InlineData("0006", "UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=N'0001_create_planning_schema.sql' WHERE Id=1")]
    [InlineData("0006", "UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=N'Wrong.Prefix.0001_create_planning_schema.sql' WHERE Id=1")]
    [InlineData("0006", "DELETE dbo.AdventuresSuiteSchemaVersions WHERE Id=3")]
    [InlineData("0006", "UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=(SELECT ScriptName FROM dbo.AdventuresSuiteSchemaVersions WHERE Id=1) WHERE Id=2")]
    [InlineData("0006", "INSERT dbo.AdventuresSuiteSchemaVersions(ScriptName,Applied) VALUES(N'AdventuresSuite.DatabaseMigrator.Database.Migrations.9999_extra.sql',SYSUTCDATETIME())")]
    [InlineData("0006", "UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=N'temporary' WHERE Id=1; UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=N'AdventuresSuite.DatabaseMigrator.Database.Migrations.0001_create_planning_schema.sql' WHERE Id=2; UPDATE dbo.AdventuresSuiteSchemaVersions SET ScriptName=N'AdventuresSuite.DatabaseMigrator.Database.Migrations.0002_create_adventure_plans.sql' WHERE Id=1")]
    [InlineData("0006", "CREATE TABLE planning.Unexpected(Id int NOT NULL)")]
    [InlineData("0006", "GRANT EXECUTE TO AdventuresSuiteAuthenticationRuntime")]
    [InlineData("0006", "CREATE USER [AdventuresSuiteMigrationDev-ffc9a] WITHOUT LOGIN")]
    public async Task BaselineReader_RejectsNoncanonicalJournal(string maximum, string? mutation)
    {
        var master = RequireConnectionString();
        var databaseName = $"AdventuresSuiteAdminBaselineInvalid_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await CreateDatabaseAsync(master, databaseName);
        try
        {
            using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
                DatabaseMigratorRunner.MigrateWithLockHeld(connectionString, maximum);
            if (mutation is not null) await ExecuteAsync(connectionString, mutation);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var result = await CaptureBaselineAsync(connection, databaseName);
            Assert.Equal(1, result.ExitCode);
            Assert.Equal("unexpected", result.Evidence.RootElement.GetProperty("outcome").GetString());
        }
        finally { await DropDatabaseAsync(master, databaseName); }
    }

    /// <summary>Consumes every baseline result set after the complete migration state through 0012.</summary>
    [Fact]
    public async Task BaselineReader_ConsumesCompleteQueryForMigratedState()
    {
        var masterConnectionString = RequireConnectionString();
        var suffix = Guid.NewGuid().ToString("N");
        var databaseName = $"AdventuresSuiteAdminBaselineComplete_{suffix}";
        var loginName = $"baseline_{suffix}";
        var password = $"Local-{Guid.NewGuid():N}!aA9";
        await ExecuteAsync(masterConnectionString,
            $"CREATE LOGIN [{loginName}] WITH PASSWORD = '{password}'; CREATE DATABASE [{databaseName}];");

        try
        {
            var connectionString = BuildDatabaseConnectionString(masterConnectionString, databaseName);
            await ExecuteAsync(connectionString,
                $"CREATE USER [{MigrationPrincipalName}] FOR LOGIN [{loginName}];");
            await ExecuteParameterizedAsync(connectionString,
                AzureDevelopmentBootstrapper.BuildMigrationGrants($"[{MigrationPrincipalName}]"),
                MigrationPrincipalName);
            Assert.Equal(12,
                (await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString)).Count);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var result = await CaptureBaselineAsync(connection, databaseName);
            Assert.True(result.ExitCode == 0, result.Evidence.RootElement.ToString());
            Assert.Equal("complete", result.Evidence.RootElement.GetProperty("outcome").GetString());
        }
        finally
        {
            await DropDatabaseAsync(masterConnectionString, databaseName);
            await ExecuteAsync(masterConnectionString, $"""
                IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name=N'{loginName}')
                    DROP LOGIN [{loginName}];
                """);
        }
    }

    private static async Task<(int ExitCode, JsonDocument Evidence)> CaptureBaselineAsync(
        SqlConnection connection, string databaseName)
    {
        var baselinePath = Path.Combine(
            FindRepositoryRoot(), "infrastructure/private-sql-admin-operation/baseline.sql");
        var baseline = await File.ReadAllTextAsync(baselinePath);
        var previousPath = Environment.GetEnvironmentVariable("ADVENTURESSUITE_ADMIN_BASELINE_SQL_PATH");
        var previousHash = Environment.GetEnvironmentVariable("ADVENTURESSUITE_ADMIN_BASELINE_SQL_SHA256");
        try
        {
            Environment.SetEnvironmentVariable("ADVENTURESSUITE_ADMIN_BASELINE_SQL_PATH", baselinePath);
            Environment.SetEnvironmentVariable(
                "ADVENTURESSUITE_ADMIN_BASELINE_SQL_SHA256",
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseline))).ToLowerInvariant());
            using var writer = new StringWriter();
            var exitCode = await SqlAdministratorOperationRunner.CaptureBaselineAsync(
                connection,
                new SqlAdministratorOperationRunner.Context(
                    Guid.Parse("d7add2bb-ac03-49a8-9377-d0bf6a012f2f"),
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("ffc9a4bd-67c4-44af-82dc-b7f663f8bea5"),
                    Guid.Parse("d0da8236-91dc-4454-8a3d-19d08a406e5d"),
                    MigrationPrincipalName,
                    "adventures-suite-dev-sql",
                    databaseName,
                    new string('a', 40),
                    "sql-admin-baseline-integration-test",
                    new string('b', 64),
                    1,
                    1,
                    new string('c', 64),
                    new string('d', 64),
                    "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                    "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/test",
                    "/subscriptions/test/resourceGroups/test/providers/Microsoft.Network/privateEndpoints/test"),
                writer);
            return (exitCode, JsonDocument.Parse(writer.ToString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADVENTURESSUITE_ADMIN_BASELINE_SQL_PATH", previousPath);
            Environment.SetEnvironmentVariable("ADVENTURESSUITE_ADMIN_BASELINE_SQL_SHA256", previousHash);
        }
    }

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        return connectionString;
    }

    private static async Task ExecuteParameterizedAsync(
        string connectionString,
        string sql,
        string alias)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AliasParameter", alias);
        await command.ExecuteNonQueryAsync();
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

    private static async Task DropDatabaseAsync(string masterConnectionString, string databaseName) =>
        await ExecuteAsync(masterConnectionString, $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END;
            """);

    private static string BuildDatabaseConnectionString(
        string masterConnectionString,
        string databaseName) =>
        new SqlConnectionStringBuilder(masterConnectionString) { InitialCatalog = databaseName }.ConnectionString;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
