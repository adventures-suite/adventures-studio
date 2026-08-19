using AdventuresSuite.DatabaseMigrator;

const string migrationConnectionVariable = "ADVENTURESSUITE_SQL_CONNECTION_STRING";
const string administratorConnectionVariable = "ADVENTURESSUITE_ADMIN_SQL_CONNECTION_STRING";

if (args is ["--list"])
{
    foreach (var resourceName in MigrationCatalog.GetOrderedResourceNames(typeof(MigrationCatalog).Assembly))
        Console.WriteLine(resourceName);
    return 0;
}

try
{
    switch (args)
    {
        case ["--admin-baseline"]:
            return await SqlAdministratorOperationRunner.RunAsync("baseline");
        case ["--admin-bootstrap"]:
            return await SqlAdministratorOperationRunner.RunAsync("bootstrap");
        case ["--admin-bootstrap-companion-policy-role"]:
            return await SqlAdministratorOperationRunner.RunAsync("bootstrap-policy-role");
        case ["--admin-cleanup"]:
            return await SqlAdministratorOperationRunner.RunAsync("cleanup");
        case ["--admin-denial-proof"]:
            return await SqlAdministratorOperationRunner.RunAsync("denial-proof");
        case ["--bootstrap-sql"]:
            await AzureDevelopmentBootstrapper.BootstrapMigrationIdentityAsync(
                RequireEnvironment(administratorConnectionVariable),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME"));
            Console.WriteLine("Migration identity bootstrap completed successfully.");
            break;
        case ["--bind-runtime"]:
            await AzureDevelopmentBootstrapper.BindRuntimeIdentityAsync(
                RequireEnvironment(administratorConnectionVariable),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_APP_PRINCIPAL_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_APP_PRINCIPAL_CLIENT_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_APP_PRINCIPAL_NAME"));
            Console.WriteLine("Runtime identity binding completed successfully.");
            break;
        case ["--verify-permissions"]:
            await AzureDevelopmentBootstrapper.VerifyMigrationPermissionsAsync(
                RequireEnvironment(migrationConnectionVariable));
            Console.WriteLine("Migration identity permission verification completed successfully.");
            break;
        case ["--run-reviewed-operation"]:
            return await MigrationOperationRunner.RunAsync(
                RequireEnvironment(migrationConnectionVariable));
        case ["--verify-execution-channel"]:
            return await MigrationExecutionModes.VerifyExecutionChannelAsync();
        case ["--capture-migration-state"]:
            return await MigrationExecutionModes.CaptureMigrationStateAsync(
                RequireEnvironment(migrationConnectionVariable));
        case ["--verify-migration-state"]:
            return await MigrationExecutionModes.VerifyMigrationStateAsync(
                RequireEnvironment(migrationConnectionVariable));
        case ["--bind-companion-read-runtime"]:
            await AzureDevelopmentBootstrapper.BindCompanionReadIdentityAsync(
                RequireEnvironment(administratorConnectionVariable),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_CLIENT_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_NAME"));
            Console.WriteLine("Companion read identity binding completed successfully.");
            break;
        case ["--verify-companion-read-permissions"]:
            await AzureDevelopmentBootstrapper.VerifyCompanionReadPermissionsAsync(
                RequireEnvironment(administratorConnectionVariable),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_CLIENT_ID"),
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_COMPANION_PRINCIPAL_NAME"));
            Console.WriteLine("Companion read permission verification completed successfully.");
            break;
        case ["--bootstrap-key-vault"]:
            await AzureDevelopmentBootstrapper.BootstrapKeyVaultAsync(
                Environment.GetEnvironmentVariable("ADVENTURESSUITE_KEY_VAULT_URI"));
            Console.WriteLine("Key Vault bootstrap completed successfully.");
            break;
        case []:
        case ["--migrate"]:
            var appliedScripts = DatabaseMigratorRunner.Migrate(
                RequireEnvironment(migrationConnectionVariable));
            Console.WriteLine(
                $"AdventuresSuite database migrations completed successfully; applied {appliedScripts.Count} script(s).");
            break;
        default:
            Console.Error.WriteLine("Specify one approved migration or bootstrap operation.");
            return 2;
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static string RequireEnvironment(string name) =>
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
        ? Environment.GetEnvironmentVariable(name)!
        : throw new InvalidOperationException($"Set {name} in the execution environment.");
