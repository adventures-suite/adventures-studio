using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Performs explicit, one-time development infrastructure bootstrap operations.</summary>
internal static class AzureDevelopmentBootstrapper
{
    private const string ApplicationUserName = "adventures-suite-dev-runtime";
    private const string MigrationUserName = "adventures-suite-dev-migrations";
    private const string RuntimeRoleName = "AdventuresSuiteAuthenticationRuntime";
    private const string WrappingKeyName = "adventures-suite-data-protection";
    private const string CertificateName = "adventures-suite-external-id";

    /// <summary>Creates only the contained migration principal using Entra-administrator authority.</summary>
    public static Task BootstrapMigrationIdentityAsync(
        string administratorConnectionString,
        string? migrationPrincipalId) =>
        ExecutePrincipalCommandAsync(
            administratorConnectionString,
            migrationPrincipalId,
            MigrationUserName,
            $"""
            ALTER ROLE [db_ddladmin] ADD MEMBER [{MigrationUserName}];
            ALTER ROLE [db_datareader] ADD MEMBER [{MigrationUserName}];
            ALTER ROLE [db_datawriter] ADD MEMBER [{MigrationUserName}];
            GRANT CONNECT TO [{MigrationUserName}];
            """);

    /// <summary>Binds the runtime principal after migrations have created the application role.</summary>
    public static Task BindRuntimeIdentityAsync(
        string administratorConnectionString,
        string? applicationPrincipalId) =>
        ExecutePrincipalCommandAsync(
            administratorConnectionString,
            applicationPrincipalId,
            ApplicationUserName,
            $"""
            IF DATABASE_PRINCIPAL_ID(N'{RuntimeRoleName}') IS NULL
                THROW 51000, 'The authentication runtime role has not been migrated.', 1;
            ALTER ROLE [{RuntimeRoleName}] ADD MEMBER [{ApplicationUserName}];
            GRANT CONNECT TO [{ApplicationUserName}];
            """);

    /// <summary>Verifies migration DDL, data, journal, and authentication-schema permissions.</summary>
    public static async Task VerifyMigrationPermissionsAsync(string migrationConnectionString)
    {
        await using var connection = new SqlConnection(migrationConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONNECT') <> 1
                OR IS_ROLEMEMBER('db_ddladmin') <> 1
                OR IS_ROLEMEMBER('db_datareader') <> 1
                OR IS_ROLEMEMBER('db_datawriter') <> 1
                OR OBJECT_ID(N'dbo.AdventuresSuiteSchemaVersions', N'U') IS NULL
                OR OBJECT_ID(N'auth.Users', N'U') IS NULL
                OR OBJECT_ID(N'auth.ExternalIdentities', N'U') IS NULL
                OR OBJECT_ID(N'auth.UserSessions', N'U') IS NULL
                THROW 51000, 'Migration permissions or schema are unavailable.', 1;

            SELECT TOP (0) ScriptName FROM dbo.AdventuresSuiteSchemaVersions;
            """;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Creates only non-exportable Key Vault cryptographic objects.</summary>
    public static async Task BootstrapKeyVaultAsync(string? keyVaultUri)
    {
        var vaultUri = ParseVaultUri(keyVaultUri);
        var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        var keyClient = new KeyClient(vaultUri, credential);
        try
        {
            await keyClient.GetKeyAsync(WrappingKeyName);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            await keyClient.CreateRsaKeyAsync(new CreateRsaKeyOptions(WrappingKeyName)
            {
                KeySize = 2048,
                Enabled = true,
                KeyOperations = { KeyOperation.WrapKey, KeyOperation.UnwrapKey }
            });
        }

        var certificateClient = new CertificateClient(vaultUri, credential);
        KeyVaultCertificateWithPolicy certificate;
        try
        {
            certificate = (await certificateClient.GetCertificateAsync(CertificateName)).Value;
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            var policy = new CertificatePolicy("Self", "CN=AdventuresSuite Development External ID")
            {
                Exportable = false,
                KeyType = CertificateKeyType.Rsa,
                KeySize = 2048,
                ReuseKey = false,
                ValidityInMonths = 12,
                KeyUsage = { CertificateKeyUsage.DigitalSignature },
                EnhancedKeyUsage = { "1.3.6.1.5.5.7.3.2" }
            };
            certificate = await (await certificateClient.StartCreateCertificateAsync(CertificateName, policy))
                .WaitForCompletionAsync();
        }

        Console.WriteLine(
            "External ID public certificate is ready; thumbprint {0}, not-before {1:O}, expires {2:O}.",
            Convert.ToHexString(certificate.Properties.X509Thumbprint ?? []),
            certificate.Properties.NotBefore,
            certificate.Properties.ExpiresOn);
        Console.WriteLine(
            "External ID public certificate DER (public material): {0}",
            Convert.ToBase64String(certificate.Cer));
    }

    private static async Task ExecutePrincipalCommandAsync(
        string connectionString,
        string? principalId,
        string principalName,
        string grants)
    {
        if (!Guid.TryParse(principalId, out var objectId))
        {
            throw new InvalidOperationException("The approved workload principal identity is required.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            DECLARE @ObjectId varchar(36) = @ObjectIdParameter;
            IF DATABASE_PRINCIPAL_ID(N'{principalName}') IS NULL
                EXEC(N'CREATE USER [{principalName}] FROM EXTERNAL PROVIDER WITH OBJECT_ID=''' + @ObjectId + N''';');
            {grants}
            """;
        command.Parameters.AddWithValue("@ObjectIdParameter", objectId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static Uri ParseVaultUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo)
            ? uri
            : throw new InvalidOperationException("The approved Key Vault URI is required.");
}
