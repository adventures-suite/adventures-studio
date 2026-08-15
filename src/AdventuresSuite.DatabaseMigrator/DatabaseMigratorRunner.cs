using System.Reflection;
using DbUp;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Executes the embedded, journaled AdventuresSuite database migrations.</summary>
public static class DatabaseMigratorRunner
{
    /// <summary>
    /// Applies pending migrations with one transaction per script and returns
    /// the script names selected before execution.
    /// </summary>
    public static IReadOnlyList<string> Migrate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A database connection string is required.", nameof(connectionString));
        }

        Func<SqlConnection> connectionFactory = () => new SqlConnection(connectionString);
        using var migrationLock = AcquireMigrationLock(connectionFactory);
        return MigrateWithLockHeld(connectionFactory);
    }

    /// <summary>Applies pending migrations while an approved caller holds the migration lock.</summary>
    internal static IReadOnlyList<string> MigrateWithLockHeld(
        string connectionString,
        string? maximumMigrationNumber = null) =>
        MigrateWithLockHeld(() => new SqlConnection(connectionString), maximumMigrationNumber);

    /// <summary>Applies pending migrations through the exact reviewed connection factory.</summary>
    internal static IReadOnlyList<string> MigrateWithLockHeld(
        Func<SqlConnection> connectionFactory,
        string? maximumMigrationNumber = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        var assembly = typeof(MigrationCatalog).Assembly;
        _ = MigrationCatalog.GetOrderedResourceNames(assembly);
        var upgrader = BuildUpgradeEngine(connectionFactory, assembly, maximumMigrationNumber);
        var pendingScripts = upgrader.GetScriptsToExecute()
            .Select(script => script.Name)
            .ToArray();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        return Array.AsReadOnly(pendingScripts);
    }

    /// <summary>Acquires the database-scoped, session-owned exclusive migrator lock.</summary>
    internal static IDisposable AcquireMigrationLock(string connectionString)
        => AcquireMigrationLock(() => new SqlConnection(connectionString));

    /// <summary>Acquires the migration lock through the exact reviewed connection factory.</summary>
    internal static IDisposable AcquireMigrationLock(Func<SqlConnection> connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        var connection = connectionFactory();
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @Result int;
                EXEC @Result = sys.sp_getapplock
                    @Resource = N'AdventuresSuite.DatabaseMigrator',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Session',
                    @LockTimeout = 0,
                    @DbPrincipal = N'public';
                SELECT @Result;
                """;
            var result = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (result < 0)
            {
                throw new InvalidOperationException("Another database migration is already running.");
            }

            return new MigrationLock(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static DbUp.Engine.UpgradeEngine BuildUpgradeEngine(
        Func<SqlConnection> connectionFactory,
        Assembly assembly,
        string? maximumMigrationNumber) =>
        DeployChanges.To
            .SqlDatabase(connectionFactory)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resourceName => MigrationCatalog.IsMigrationResource(assembly, resourceName)
                    && (maximumMigrationNumber is null
                        || string.CompareOrdinal(
                            resourceName[(resourceName.LastIndexOf(".Database.Migrations.", StringComparison.Ordinal)
                                + ".Database.Migrations.".Length)..][..4],
                            maximumMigrationNumber) <= 0))
            .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

    private sealed class MigrationLock(SqlConnection connection) : IDisposable
    {
        private SqlConnection? connection = connection;

        public void Dispose()
        {
            // Closing the owning session releases the lock even after failures.
            Interlocked.Exchange(ref connection, null)?.Dispose();
        }
    }
}
