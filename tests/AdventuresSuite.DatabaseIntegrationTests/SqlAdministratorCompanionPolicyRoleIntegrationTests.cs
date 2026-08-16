using System.Text.Json;
using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Verifies the fixed, authority-free Companion policy runtime-role bootstrap.</summary>
public sealed class SqlAdministratorCompanionPolicyRoleIntegrationTests
{
    private const string ConnectionVariable = "ADVENTURESSUITE_SQL_TEST_CONNECTION_STRING";
    private const string RoleName = "AdventuresSuiteCompanionPolicyRuntime";

    /// <summary>Creates only the absent fixed role and emits bounded evidence.</summary>
    [Fact]
    public async Task AbsentRole_IsCreatedWithoutAuthority_AndReadRoleIsUnchanged()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            var readBefore = await ReadSignatureAsync(connectionString, "AdventuresSuiteCompanionReadRuntime");
            await ExecuteAsync(connectionString, "CREATE USER [UnrelatedPrincipal] WITHOUT LOGIN;");

            var evidence = await RunAsync(connectionString);

            Assert.Equal("created", evidence.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(RoleName, evidence.RootElement.GetProperty("roleName").GetString());
            Assert.Equal("dbo", evidence.RootElement.GetProperty("owner").GetString());
            Assert.Equal(0, evidence.RootElement.GetProperty("memberCount").GetInt32());
            Assert.Equal(0, evidence.RootElement.GetProperty("parentRoleCount").GetInt32());
            Assert.Equal(0, evidence.RootElement.GetProperty("explicitPermissionCount").GetInt32());
            Assert.Equal(0, evidence.RootElement.GetProperty("ownedSecurableCount").GetInt32());
            Assert.Equal(readBefore,
                await ReadSignatureAsync(connectionString, "AdventuresSuiteCompanionReadRuntime"));
            Assert.Equal(1, await ScalarAsync(connectionString,
                "SELECT COUNT(*) FROM sys.database_principals WHERE name=N'UnrelatedPrincipal';"));
        });
    }

    /// <summary>Accepts an exact, already conforming role without changing it.</summary>
    [Fact]
    public async Task ConformingPreexistingRole_IsIdempotent()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString, $"CREATE ROLE [{RoleName}] AUTHORIZATION [dbo];");
            var before = await ReadSignatureAsync(connectionString, RoleName);

            var evidence = await RunAsync(connectionString);

            Assert.Equal("preexisting", evidence.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(before, await ReadSignatureAsync(connectionString, RoleName));
        });
    }

    /// <summary>Rejects a same-name role owned by any principal other than dbo.</summary>
    [Fact]
    public async Task WrongOwner_FailsWithoutRepair()
    {
        await AssertRejectedAsync($"""
            CREATE USER [PolicyRoleOwner] WITHOUT LOGIN;
            CREATE ROLE [{RoleName}] AUTHORIZATION [PolicyRoleOwner];
            """);
    }

    /// <summary>Rejects a same-name non-role principal instead of replacing it.</summary>
    [Theory]
    [InlineData("user")]
    [InlineData("application-role")]
    public async Task SameNameNonDatabaseRole_FailsWithoutRepair(string kind)
    {
        var sql = kind == "user"
            ? $"CREATE USER [{RoleName}] WITHOUT LOGIN;"
            : string.Concat(
                $"CREATE APPLICATION ROLE [{RoleName}] WITH PASS",
                "WORD=N'Fictional-local-only-Aa9!';");
        await AssertRejectedAsync(sql);
    }

    /// <summary>Rejects direct, broad, fixed-role-derived, and owned-securable authority.</summary>
    [Theory]
    [InlineData("direct")]
    [InlineData("control")]
    [InlineData("fixed-role")]
    [InlineData("owned-schema")]
    public async Task AnyApplicationAuthority_FailsWithoutRepair(string kind)
    {
        var mutation = kind switch
        {
            "direct" => $"GRANT SELECT ON OBJECT::planning.AdventurePlans TO [{RoleName}];",
            "control" => $"GRANT CONTROL TO [{RoleName}];",
            "fixed-role" => $"ALTER ROLE [db_datareader] ADD MEMBER [{RoleName}];",
            "owned-schema" => $"EXEC(N'CREATE SCHEMA [PolicyOwned] AUTHORIZATION [{RoleName}]');",
            _ => throw new InvalidOperationException()
        };
        await AssertRejectedAsync($"CREATE ROLE [{RoleName}] AUTHORIZATION [dbo]; {mutation}");
    }

    /// <summary>Rejects both members of the role and membership of the role in a parent.</summary>
    [Theory]
    [InlineData("member")]
    [InlineData("parent")]
    public async Task AnyRoleMembership_FailsWithoutRepair(string kind)
    {
        var mutation = kind == "member"
            ? $"CREATE USER [PolicyMember] WITHOUT LOGIN; ALTER ROLE [{RoleName}] ADD MEMBER [PolicyMember];"
            : $"CREATE ROLE [PolicyParent] AUTHORIZATION [dbo]; ALTER ROLE [PolicyParent] ADD MEMBER [{RoleName}];";
        await AssertRejectedAsync($"CREATE ROLE [{RoleName}] AUTHORIZATION [dbo]; {mutation}");
    }

    /// <summary>Rejects case-altered confusable names under a case-insensitive database collation.</summary>
    [Fact]
    public async Task CaseAlteredRoleName_FailsWithoutCreatingExactRole()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString,
                "CREATE ROLE [adventuresSuiteCompanionPolicyRuntime] AUTHORIZATION [dbo];");
            await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(connectionString));
            Assert.Equal(1, await ScalarAsync(connectionString, """
                SELECT COUNT(*) FROM sys.database_principals
                WHERE name=N'adventuresSuiteCompanionPolicyRuntime';
                """));
        });
    }

    /// <summary>Propagates cancellation before SQL mutation.</summary>
    [Fact]
    public async Task Cancellation_PropagatesWithoutCreatingRole()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await using var connection = new SqlConnection(connectionString);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SqlAdministratorOperationRunner.BootstrapCompanionPolicyRuntimeRoleAsync(
                    connection, Context(), "support-0001", "correlation-0001",
                    new StringWriter(), cancellation.Token));
            Assert.Equal(0, await ScalarAsync(connectionString,
                $"SELECT COUNT(*) FROM sys.database_principals WHERE name=N'{RoleName}';"));
        });
    }

    /// <summary>Surfaces SQL failure and never redirects to another database.</summary>
    [Fact]
    public async Task MissingDatabase_PropagatesSqlFailure()
    {
        var master = RequireConnectionString();
        var missing = $"AdventuresSuitePolicyRoleMissing_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, missing);
        await using var connection = new SqlConnection(connectionString);

        await Assert.ThrowsAsync<SqlException>(() =>
            SqlAdministratorOperationRunner.BootstrapCompanionPolicyRuntimeRoleAsync(
                connection, Context(), "support-0001", "correlation-0001", new StringWriter()));
    }

    /// <summary>Fails the operation when bounded evidence cannot be delivered.</summary>
    [Fact]
    public async Task EvidenceFailure_FailsOperation()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await using var connection = new SqlConnection(connectionString);
            await Assert.ThrowsAsync<IOException>(() =>
                SqlAdministratorOperationRunner.BootstrapCompanionPolicyRuntimeRoleAsync(
                    connection, Context(), "support-0001", "correlation-0001",
                    new ThrowingTextWriter()));
        });
    }

    /// <summary>Rejects malformed evidence identities before opening SQL.</summary>
    [Theory]
    [InlineData("bad", "correlation-0001")]
    [InlineData("support-0001", "case altered")]
    public async Task InvalidEvidenceIdentifiers_FailBeforeSql(string supportId, string correlationId)
    {
        await using var connection = new SqlConnection("Server=invalid.example;Database=invalid;");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            SqlAdministratorOperationRunner.BootstrapCompanionPolicyRuntimeRoleAsync(
                connection, Context(), supportId, correlationId, new StringWriter()));
    }

    private static async Task AssertRejectedAsync(string setupSql)
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await ExecuteAsync(connectionString, setupSql);
            var before = await ReadSignatureAsync(connectionString, RoleName);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(connectionString));
            Assert.Equal(before, await ReadSignatureAsync(connectionString, RoleName));
        });
    }

    private static async Task<JsonDocument> RunAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        using var writer = new StringWriter();
        Assert.Equal(0, await SqlAdministratorOperationRunner.BootstrapCompanionPolicyRuntimeRoleAsync(
            connection, Context(), "support-0001", "correlation-0001", writer));
        return JsonDocument.Parse(writer.ToString());
    }

    private static SqlAdministratorOperationRunner.Context Context() => new(
        Guid.Parse("d7add2bb-ac03-49a8-9377-d0bf6a012f2f"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("ffc9a4bd-67c4-44af-82dc-b7f663f8bea5"),
        Guid.Parse("d0da8236-91dc-4454-8a3d-19d08a406e5d"),
        "AdventuresSuiteMigrationDev-ffc9a",
        "adventures-suite-dev-sql",
        "DisposableDatabase",
        new string('a', 40),
        "policy-role-bootstrap-test",
        new string('b', 64),
        12345678,
        23456789,
        new string('c', 64),
        new string('d', 64),
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.Sql/servers/test",
        "/subscriptions/test/resourceGroups/test/providers/Microsoft.Network/privateEndpoints/test");

    private static async Task WithDatabaseAsync(Func<string, Task> test)
    {
        var master = RequireConnectionString();
        var databaseName = $"AdventuresSuitePolicyRole_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(master, databaseName);
        await ExecuteAsync(master, $"CREATE DATABASE [{databaseName}];");
        try
        {
            Assert.Equal(9, DatabaseMigratorRunner.Migrate(connectionString).Count);
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

    private static async Task<string> ReadSignatureAsync(string connectionString, string roleName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT principal.name, principal.type, owner.name, principal.is_fixed_role,
                (SELECT COUNT_BIG(*) FROM sys.database_role_members WHERE role_principal_id=principal.principal_id),
                (SELECT COUNT_BIG(*) FROM sys.database_role_members WHERE member_principal_id=principal.principal_id),
                (SELECT COUNT_BIG(*) FROM sys.database_permissions WHERE grantee_principal_id=principal.principal_id),
                (SELECT COUNT_BIG(*) FROM sys.schemas WHERE principal_id=principal.principal_id),
                (SELECT COUNT_BIG(*) FROM sys.objects WHERE principal_id=principal.principal_id)
            FROM sys.database_principals AS principal
            LEFT JOIN sys.database_principals AS owner ON owner.principal_id=principal.owning_principal_id
            WHERE principal.name COLLATE Latin1_General_100_BIN2=@RoleName COLLATE Latin1_General_100_BIN2;
        """, connection);
        command.Parameters.Add("@RoleName", System.Data.SqlDbType.NVarChar, 128).Value = roleName;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return string.Empty;
        return string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(index =>
            Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture)));
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
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} for the SQL integration gate.");
        return connectionString;
    }

    private static string BuildDatabaseConnectionString(string master, string databaseName) =>
        new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;

    private sealed class ThrowingTextWriter : StringWriter
    {
        public override Task WriteLineAsync(string? value) =>
            Task.FromException(new IOException("Fictional evidence sink failure."));
    }
}
