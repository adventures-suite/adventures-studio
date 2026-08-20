using AdventuresSuite.DatabaseMigrator;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseIntegrationTests;

/// <summary>Applies the reviewed catalog around the administrator-owned 0010 role prerequisite.</summary>
internal static class CompanionPolicyMigrationTestHarness
{
    /// <summary>Applies 0001-0009, provisions the authority-free role, then applies 0010-0013.</summary>
    internal static async Task<IReadOnlyList<string>> MigrateAllAsync(string connectionString)
    {
        var applied = new List<string>();
        using (DatabaseMigratorRunner.AcquireMigrationLock(connectionString))
        {
            applied.AddRange(DatabaseMigratorRunner.MigrateWithLockHeld(
                connectionString, maximumMigrationNumber: "0009"));
        }

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                IF DATABASE_PRINCIPAL_ID(N'AdventuresSuiteCompanionPolicyRuntime') IS NULL
                    CREATE ROLE AdventuresSuiteCompanionPolicyRuntime AUTHORIZATION dbo;
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        applied.AddRange(DatabaseMigratorRunner.Migrate(connectionString));
        return applied.AsReadOnly();
    }
}
