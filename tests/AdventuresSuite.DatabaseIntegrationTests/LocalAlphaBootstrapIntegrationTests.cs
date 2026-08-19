using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the local Alpha bootstrap against real migrated SQL.</summary>
public sealed class LocalAlphaBootstrapIntegrationTests
{
    /// <summary>Ensures repeated bootstrap preserves one exact minimum-permission membership.</summary>
    [Fact]
    public async Task Bootstrap_SecondRunIsExactNoOp()
    {
        var master = Environment.GetEnvironmentVariable("ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(master));
        var databaseName = $"AlphaBootstrap_{Guid.NewGuid():N}";
        var database = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteAsync(master!, $"CREATE DATABASE [{databaseName}];");
        try
        {
            await CompanionPolicyMigrationTestHarness.MigrateAllAsync(database);
            await LocalAlphaBootstrap.BootstrapApprovedTargetAsync(database);
            var first = await SignatureAsync(database);
            await LocalAlphaBootstrap.BootstrapApprovedTargetAsync(database);
            var second = await SignatureAsync(database);

            Assert.Equal("1|1|1|Planner|0|1", first);
            Assert.Equal(first, second);
        }
        finally
        {
            await ExecuteAsync(master!, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static async Task<string> SignatureAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONCAT(
                (SELECT COUNT(*) FROM auth.Users WHERE UserId='user_local_alpha_planner'),'|',
                (SELECT COUNT(*) FROM auth.ExternalIdentities WHERE ExternalIdentityId='identity_local_alpha_planner'),'|',
                (SELECT COUNT(*) FROM auth.CreatorMemberships WHERE CreatorId='creator_local_alpha'),'|',
                (SELECT TOP 1 Role FROM auth.CreatorMembershipRoles WHERE CreatorId='creator_local_alpha'),'|',
                (SELECT COUNT(*) FROM auth.CreatorMembershipPermissionGrants WHERE CreatorId='creator_local_alpha'),'|',
                (SELECT COUNT(*) FROM audit.AuditEvents WHERE AuditEventId='audit_local_alpha_bootstrap'));
            """;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
