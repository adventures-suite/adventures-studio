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
    if (string.Equals(
            Environment.GetEnvironmentVariable("ADVENTURESSUITE_BOOTSTRAP_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase))
    {
        await AzureDevelopmentBootstrapper.RunAsync(
            connectionString,
            Environment.GetEnvironmentVariable("ADVENTURESSUITE_APP_PRINCIPAL_ID"),
            Environment.GetEnvironmentVariable("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID"),
            Environment.GetEnvironmentVariable("ADVENTURESSUITE_KEY_VAULT_URI"));
    }

    Console.WriteLine(
        $"AdventuresSuite database migrations completed successfully; applied {appliedScripts.Count} script(s).");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
