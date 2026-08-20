using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the one-time development Creator initial-owner bootstrap.</summary>
public sealed class SqlAdministratorInitialOwnerIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private const string UserId = "user_development_owner";

    /// <summary>Creates exactly one Owner membership and matching system audit evidence.</summary>
    [Fact]
    public async Task EmptyCreatorAndActiveUser_CreateAtomicInitialOwner()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedUserAsync(connectionString, active: true, includeIdentity: true);

            using var evidence = await RunAsync(connectionString);

            var root = evidence.RootElement;
            Assert.Equal("development-initial-owner-bootstrap-v1", root.GetProperty("operation").GetString());
            Assert.Equal("created", root.GetProperty("outcome").GetString());
            Assert.Equal(SqlAdministratorOperationRunner.InitialOwnerCreatorId,
                root.GetProperty("creatorId").GetString());
            Assert.Equal("Owner", root.GetProperty("role").GetString());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UserId))).ToLowerInvariant(),
                root.GetProperty("targetUserIdSha256").GetString());
            Assert.Equal(1, await ScalarAsync(connectionString, """
                SELECT COUNT(*) FROM auth.CreatorMemberships AS membership
                JOIN auth.CreatorMembershipRoles AS role
                  ON role.CreatorId=membership.CreatorId
                 AND role.CreatorMembershipId=membership.CreatorMembershipId
                WHERE membership.CreatorId='creator_tsa_01'
                  AND membership.CreatorMembershipId='membership_tsa_initial_owner'
                  AND membership.UserId='user_development_owner'
                  AND membership.Status='Active' AND membership.Version=1
                  AND membership.ExpiresAtUtc IS NULL AND role.Role='Owner';
                """));
            Assert.Equal(1, await ScalarAsync(connectionString, """
                SELECT COUNT(*) FROM audit.AuditEvents
                WHERE AuditEventId='audit_tsa_initial_owner'
                  AND CreatorId='creator_tsa_01' AND ActorType='System' AND ActorUserId IS NULL
                  AND Permission='Creator.ManageMembers' AND ResourceType='CreatorMembership'
                  AND ResourceId='membership_tsa_initial_owner' AND Outcome='Succeeded'
                  AND ReasonCategory='Completed' AND PreviousVersion IS NULL AND ResultingVersion=1;
                """));
            Assert.Equal(0, await ScalarAsync(connectionString, """
                SELECT COUNT(*) FROM auth.CreatorMembershipPermissionGrants
                WHERE CreatorId='creator_tsa_01';
                """));
        });
    }

    /// <summary>Rejects missing, disabled, or unmapped platform identities without mutation.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task IneligibleUser_IsRejectedWithoutMembership(bool active, bool includeIdentity)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedUserAsync(connectionString, active, includeIdentity);
            await using var connection = new SqlConnection(connectionString);

            await Assert.ThrowsAsync<SqlException>(() =>
                SqlAdministratorOperationRunner.BootstrapInitialOwnerAsync(
                    connection, Context(), UserId, "support-alpha-01", "correlation-alpha-01",
                    new StringWriter()));

            Assert.Equal(0, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM auth.CreatorMemberships WHERE CreatorId='creator_tsa_01';"));
            Assert.Equal(0, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE AuditEventId='audit_tsa_initial_owner';"));
        });
    }

    /// <summary>Refuses to operate when any membership state already exists for the Creator.</summary>
    [Fact]
    public async Task ExistingCreatorMembership_RejectsBootstrapWithoutRepair()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedUserAsync(connectionString, active: true, includeIdentity: true);
            await ExecuteAsync(connectionString, """
                DECLARE @Now datetimeoffset(7)=SYSUTCDATETIME();
                INSERT auth.CreatorMemberships
                    (CreatorId,CreatorMembershipId,UserId,Status,Version,EffectiveFromUtc,ExpiresAtUtc,
                     CreatedAtUtc,UpdatedAtUtc,CreatedByUserId,UpdatedByUserId)
                VALUES ('creator_tsa_01','membership_existing','user_development_owner','Active',1,
                    @Now,NULL,@Now,@Now,'user_development_owner','user_development_owner');
                INSERT auth.CreatorMembershipRoles (CreatorId,CreatorMembershipId,Role)
                    VALUES ('creator_tsa_01','membership_existing','Viewer');
                """);

            await using var connection = new SqlConnection(connectionString);
            await Assert.ThrowsAsync<SqlException>(() =>
                SqlAdministratorOperationRunner.BootstrapInitialOwnerAsync(
                    connection, Context(), UserId, "support-alpha-01", "correlation-alpha-01",
                    new StringWriter()));

            Assert.Equal(1, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM auth.CreatorMemberships WHERE CreatorId='creator_tsa_01';"));
            Assert.Equal(0, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM audit.AuditEvents WHERE AuditEventId='audit_tsa_initial_owner';"));
        });
    }

    /// <summary>Rolls back membership state when required audit insertion fails.</summary>
    [Fact]
    public async Task AuditInsertionFailure_RollsBackMembershipAndRole()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await SeedUserAsync(connectionString, active: true, includeIdentity: true);
            await ExecuteAsync(connectionString, """
                CREATE TRIGGER audit.RejectInitialOwnerAudit ON audit.AuditEvents
                INSTEAD OF INSERT AS
                BEGIN
                    THROW 51000, 'Fictional audit rejection.', 1;
                END;
                """);
            await using var connection = new SqlConnection(connectionString);

            await Assert.ThrowsAsync<SqlException>(() =>
                SqlAdministratorOperationRunner.BootstrapInitialOwnerAsync(
                    connection, Context(), UserId, "support-alpha-01", "correlation-alpha-01",
                    new StringWriter()));

            Assert.Equal(0, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM auth.CreatorMemberships WHERE CreatorId='creator_tsa_01';"));
            Assert.Equal(0, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM auth.CreatorMembershipRoles WHERE CreatorId='creator_tsa_01';"));
        });
    }

    /// <summary>Rejects malformed user and evidence identifiers before opening SQL.</summary>
    [Theory]
    [InlineData("User_Invalid", "support-alpha-01", "correlation-alpha-01")]
    [InlineData("user_valid", "bad", "correlation-alpha-01")]
    [InlineData("user_valid", "support-alpha-01", "case altered")]
    public async Task InvalidInputs_FailBeforeSql(string userId, string supportId, string correlationId)
    {
        await using var connection = new SqlConnection("Server=invalid.example;Database=invalid;");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            SqlAdministratorOperationRunner.BootstrapInitialOwnerAsync(
                connection, Context(), userId, supportId, correlationId, new StringWriter()));
    }

    private static async Task<JsonDocument> RunAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        using var writer = new StringWriter();
        Assert.Equal(0, await SqlAdministratorOperationRunner.BootstrapInitialOwnerAsync(
            connection, Context(), UserId, "support-alpha-01", "correlation-alpha-01", writer));
        return JsonDocument.Parse(writer.ToString());
    }

    private static SqlAdministratorOperationRunner.Context Context() => new(
        Guid.Parse("d7add2bb-ac03-49a8-9377-d0bf6a012f2f"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("ffc9a4bd-67c4-44af-82dc-b7f663f8bea5"),
        Guid.Parse("d0da8236-91dc-4454-8a3d-19d08a406e5d"),
        "AdventuresSuiteMigrationDev-ffc9a", "adventures-suite-dev-sql", "DisposableDatabase",
        new string('a', 40), "initial-owner-bootstrap-test", new string('b', 64),
        12345678, 23456789, new string('c', 64), new string('d', 64),
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/test",
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.Network/privateEndpoints/test");

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var master = RequireConnectionString();
        var databaseName = $"AdventuresSuiteInitialOwner_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            await CompanionPolicyMigrationTestHarness.MigrateAllAsync(connectionString);
            await test(connectionString);
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

    private static async Task SeedUserAsync(string connectionString, bool active, bool includeIdentity)
    {
        var status = active ? "Active" : "Disabled";
        var disabled = active ? "NULL" : "SYSUTCDATETIME()";
        await ExecuteAsync(connectionString, $"""
            DECLARE @Now datetimeoffset(7)=SYSUTCDATETIME();
            INSERT auth.Users (UserId,Status,SecurityVersion,CreatedAtUtc,UpdatedAtUtc,DisabledAtUtc)
                VALUES ('{UserId}','{status}',1,@Now,@Now,{disabled});
            {(includeIdentity ? $"""
            INSERT auth.ExternalIdentities
                (ExternalIdentityId,UserId,Provider,Issuer,Subject,CreatedAtUtc,LastAuthenticatedAtUtc,DisabledAtUtc)
            VALUES ('identity_development_owner','{UserId}','external_id',
                'https://fictional.example/tenant','fictional-subject',@Now,@Now,NULL);
            """ : string.Empty)}
            """);
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), $"Set {ConnectionVariable} for the SQL integration gate.");
        return connectionString;
    }

    private static string BuildDatabaseConnectionString(string master, string databaseName) =>
        new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
}
