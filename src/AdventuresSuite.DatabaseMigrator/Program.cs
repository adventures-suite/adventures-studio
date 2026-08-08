using AdventuresSuite.DatabaseMigrator;
const string connectionVariable = "ADVENTURESSUITE_SQL_CONNECTION_STRING";
if (args is ["--list"])
{
    foreach (var resourceName in MigrationCatalog.GetOrderedResourceNames(
        typeof(MigrationCatalog).Assembly))
    {
        Console.WriteLine(resourceName);
    }

    return 0;
}

var connectionString = Environment.GetEnvironmentVariable(connectionVariable);

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Set {connectionVariable} in the execution environment.");
    return 2;
}

try
{
    var appliedScripts = DatabaseMigratorRunner.Migrate(connectionString);
    Console.WriteLine(
        $"AdventuresSuite database migrations completed successfully; applied {appliedScripts.Count} script(s).");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
