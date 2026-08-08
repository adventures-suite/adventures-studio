using System.Reflection;
using DbUp;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Executes the embedded, journaled AdventuresSuite database migrations.</summary>
public static class DatabaseMigratorRunner
{
    /// <summary>
    /// Applies pending migrations transactionally and returns the script names
    /// selected before execution.
    /// </summary>
    public static IReadOnlyList<string> Migrate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A database connection string is required.", nameof(connectionString));
        }

        var assembly = typeof(MigrationCatalog).Assembly;
        _ = MigrationCatalog.GetOrderedResourceNames(assembly);
        var upgrader = BuildUpgradeEngine(connectionString, assembly);
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

    private static DbUp.Engine.UpgradeEngine BuildUpgradeEngine(
        string connectionString,
        Assembly assembly) =>
        DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resourceName => MigrationCatalog.IsMigrationResource(assembly, resourceName))
            .JournalToSqlTable("dbo", "AdventuresSuiteSchemaVersions")
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();
}
