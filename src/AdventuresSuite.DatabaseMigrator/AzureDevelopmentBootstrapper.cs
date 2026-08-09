using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>
/// Performs the approved one-time development bootstrap through the private
/// migration workload identity.
/// </summary>
internal static class AzureDevelopmentBootstrapper
{
    private const string ApplicationUserName = "adventures-suite-dev-runtime";
    private const string MigrationUserName = "adventures-suite-dev-migrations";
    private const string RuntimeRoleName = "adventures_suite_auth_runtime";
    private const string WrappingKeyName = "adventures-suite-data-protection";
    private const string CertificateName = "adventures-suite-external-id";

    public static async Task RunAsync(
        string connectionString,
        string? applicationPrincipalId,
        string? migrationPrincipalId,
        string? keyVaultUri)
    {
        if (!Guid.TryParse(applicationPrincipalId, out var applicationObjectId)
            || !Guid.TryParse(migrationPrincipalId, out var migrationObjectId)
            || !Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri)
            || vaultUri.Scheme != Uri.UriSchemeHttps
            || vaultUri.AbsolutePath != "/")
        {
            throw new InvalidOperationException("The approved bootstrap identities and Key Vault URI are required.");
        }

        await BootstrapSqlAsync(
            connectionString,
            applicationObjectId,
            migrationObjectId);
        await BootstrapKeyVaultAsync(vaultUri);
        Console.WriteLine("Azure development bootstrap completed successfully.");
    }

    private static async Task BootstrapSqlAsync(
        string connectionString,
        Guid applicationObjectId,
        Guid migrationObjectId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @ApplicationObjectId varchar(36) = @ApplicationObjectIdParameter;
            DECLARE @MigrationObjectId varchar(36) = @MigrationObjectIdParameter;

            IF DATABASE_PRINCIPAL_ID(N'adventures-suite-dev-runtime') IS NULL
                EXEC(N'CREATE USER [adventures-suite-dev-runtime] FROM EXTERNAL PROVIDER WITH OBJECT_ID=''' + @ApplicationObjectId + N''';');
            IF DATABASE_PRINCIPAL_ID(N'adventures-suite-dev-migrations') IS NULL
                EXEC(N'CREATE USER [adventures-suite-dev-migrations] FROM EXTERNAL PROVIDER WITH OBJECT_ID=''' + @MigrationObjectId + N''';');
            IF DATABASE_PRINCIPAL_ID(N'adventures_suite_auth_runtime') IS NULL
                CREATE ROLE [adventures_suite_auth_runtime];

            ALTER ROLE [adventures_suite_auth_runtime] ADD MEMBER [adventures-suite-dev-runtime];
            GRANT CONNECT TO [adventures-suite-dev-runtime];
            GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth] TO [adventures_suite_auth_runtime];

            ALTER ROLE [db_ddladmin] ADD MEMBER [adventures-suite-dev-migrations];
            ALTER ROLE [db_datareader] ADD MEMBER [adventures-suite-dev-migrations];
            ALTER ROLE [db_datawriter] ADD MEMBER [adventures-suite-dev-migrations];
            GRANT CONNECT TO [adventures-suite-dev-migrations];
            """;
        command.Parameters.AddWithValue("@ApplicationObjectIdParameter", applicationObjectId.ToString());
        command.Parameters.AddWithValue("@MigrationObjectIdParameter", migrationObjectId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task BootstrapKeyVaultAsync(Uri vaultUri)
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = true
        });
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
                KeyOperations =
                {
                    KeyOperation.WrapKey,
                    KeyOperation.UnwrapKey
                }
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
                ContentType = CertificateContentType.Pkcs12,
                Exportable = true,
                KeyType = CertificateKeyType.Rsa,
                KeySize = 2048,
                ReuseKey = false,
                ValidityInMonths = 12,
                KeyUsage = { CertificateKeyUsage.DigitalSignature },
                EnhancedKeyUsage = { "1.3.6.1.5.5.7.3.2" }
            };
            var operation = await certificateClient.StartCreateCertificateAsync(CertificateName, policy);
            certificate = await operation.WaitForCompletionAsync();
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
}
