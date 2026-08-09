using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Performs explicit, one-time development infrastructure bootstrap operations.</summary>
internal static class AzureDevelopmentBootstrapper
{
    private const string RuntimeRoleName = "AdventuresSuiteAuthenticationRuntime";
    private const string WrappingKeyName = "adventures-suite-data-protection";
    private const string CertificateName = "adventures-suite-external-id";

    /// <summary>Creates only the contained migration principal using Entra-administrator authority.</summary>
    public static Task BootstrapMigrationIdentityAsync(
        string administratorConnectionString,
        string? migrationPrincipalId,
        string? migrationPrincipalClientId,
        string? migrationPrincipalName) =>
        ExecutePrincipalCommandAsync(
            administratorConnectionString,
            migrationPrincipalId,
            migrationPrincipalClientId,
            migrationPrincipalName,
            BuildMigrationGrants);

    /// <summary>Builds the migration-only grants for an approved contained principal alias.</summary>
    internal static string BuildMigrationGrants(string principalAlias) => $"""
        ALTER USER {principalAlias} WITH DEFAULT_SCHEMA = [dbo];
        IF ISNULL(IS_ROLEMEMBER(N'db_ddladmin', @AliasParameter), 0) <> 1
            ALTER ROLE [db_ddladmin] ADD MEMBER {principalAlias};
        IF ISNULL(IS_ROLEMEMBER(N'db_datareader', @AliasParameter), 0) <> 1
            ALTER ROLE [db_datareader] ADD MEMBER {principalAlias};
        IF ISNULL(IS_ROLEMEMBER(N'db_datawriter', @AliasParameter), 0) <> 1
            ALTER ROLE [db_datawriter] ADD MEMBER {principalAlias};
        GRANT CONNECT TO {principalAlias};
        """;

    /// <summary>Binds the runtime principal after migrations have created the application role.</summary>
    public static Task BindRuntimeIdentityAsync(
        string administratorConnectionString,
        string? applicationPrincipalId,
        string? applicationPrincipalClientId,
        string? applicationPrincipalName) =>
        ExecutePrincipalCommandAsync(
            administratorConnectionString,
            applicationPrincipalId,
            applicationPrincipalClientId,
            applicationPrincipalName,
            principalAlias => $"""
                IF DATABASE_PRINCIPAL_ID(N'{RuntimeRoleName}') IS NULL
                    THROW 51000, 'The authentication runtime role has not been migrated.', 1;
                IF ISNULL(IS_ROLEMEMBER(N'{RuntimeRoleName}', @AliasParameter), 0) <> 1
                    ALTER ROLE [{RuntimeRoleName}] ADD MEMBER {principalAlias};
                GRANT CONNECT TO {principalAlias};
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
        string? principalClientId,
        string? principalName,
        Func<string, string> buildGrants)
    {
        if (!Guid.TryParse(principalId, out var objectId)
            || !Guid.TryParse(principalClientId, out var clientId))
        {
            throw new InvalidOperationException(
                "The approved workload principal object and client identities are required.");
        }

        var alias = CreatePrincipalAlias(principalName, objectId);
        var quotedAlias = QuoteIdentifier(alias);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SET XACT_ABORT ON;
            DECLARE @PrincipalId int = DATABASE_PRINCIPAL_ID(@AliasParameter);
            IF @PrincipalId IS NULL
            BEGIN
                EXEC(N'CREATE USER {quotedAlias} FROM EXTERNAL PROVIDER WITH OBJECT_ID='''
                    + @ObjectIdParameter + N''';');
                SET @PrincipalId = DATABASE_PRINCIPAL_ID(@AliasParameter);
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.database_principals
                WHERE principal_id = @PrincipalId
                    AND type = 'E'
                    AND CAST(sid AS uniqueidentifier) = @ClientIdParameter)
                THROW 51000, 'The existing database principal does not match the approved workload identity.', 1;

            {buildGrants(quotedAlias)}
            """;
        command.Parameters.AddWithValue("@ObjectIdParameter", objectId.ToString());
        command.Parameters.AddWithValue("@ClientIdParameter", clientId);
        command.Parameters.AddWithValue("@AliasParameter", alias);
        try
        {
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            if (transaction.Connection is not null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
    }

    /// <summary>Creates the Azure SQL alias required for an exact Entra workload object ID.</summary>
    internal static string CreatePrincipalAlias(string? principalName, Guid objectId)
    {
        if (string.IsNullOrWhiteSpace(principalName)
            || principalName != principalName.Trim()
            || principalName.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new InvalidOperationException("The approved workload principal display name is required.");
        }

        var alias = $"{principalName}-{objectId:N}"[..(principalName.Length + 6)];
        return alias.Length <= 128
            ? alias
            : throw new InvalidOperationException("The workload principal SQL alias exceeds sysname limits.");
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

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
