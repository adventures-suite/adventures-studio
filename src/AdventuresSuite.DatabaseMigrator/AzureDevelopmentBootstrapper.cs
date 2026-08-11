using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Performs explicit, one-time development infrastructure bootstrap operations.</summary>
internal static class AzureDevelopmentBootstrapper
{
    private const string RuntimeRoleName = "AdventuresSuiteAuthenticationRuntime";
    private const string MembershipRuntimeRoleName = "AdventuresSuiteMembershipRuntime";
    private const string CompanionReadRuntimeRoleName = "AdventuresSuiteCompanionReadRuntime";
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
        IF DATABASE_PRINCIPAL_ID(N'{RuntimeRoleName}') IS NOT NULL
            AND NOT EXISTS (
                SELECT 1
                FROM sys.database_principals
                WHERE name = N'{RuntimeRoleName}' AND type = 'R')
            THROW 51000, 'The authentication runtime principal name is not an approved database role.', 1;
        IF DATABASE_PRINCIPAL_ID(N'{RuntimeRoleName}') IS NULL
            CREATE ROLE [{RuntimeRoleName}] AUTHORIZATION [dbo];
        IF DATABASE_PRINCIPAL_ID(N'{MembershipRuntimeRoleName}') IS NOT NULL
            AND NOT EXISTS (
                SELECT 1
                FROM sys.database_principals
                WHERE name = N'{MembershipRuntimeRoleName}' AND type = 'R')
            THROW 51000, 'The membership runtime principal name is not an approved database role.', 1;
        IF DATABASE_PRINCIPAL_ID(N'{MembershipRuntimeRoleName}') IS NULL
            CREATE ROLE [{MembershipRuntimeRoleName}] AUTHORIZATION [dbo];

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
                IF DATABASE_PRINCIPAL_ID(N'{MembershipRuntimeRoleName}') IS NULL
                    THROW 51000, 'The membership runtime role has not been migrated.', 1;
                IF ISNULL(IS_ROLEMEMBER(N'{RuntimeRoleName}', @AliasParameter), 0) <> 1
                    ALTER ROLE [{RuntimeRoleName}] ADD MEMBER {principalAlias};
                IF ISNULL(IS_ROLEMEMBER(N'{MembershipRuntimeRoleName}', @AliasParameter), 0) <> 1
                    ALTER ROLE [{MembershipRuntimeRoleName}] ADD MEMBER {principalAlias};
                GRANT CONNECT TO {principalAlias};
                """);

    /// <summary>Binds only the Companion API principal to its migrated read-only role.</summary>
    public static Task BindCompanionReadIdentityAsync(
        string administratorConnectionString,
        string? applicationPrincipalId,
        string? applicationPrincipalClientId,
        string? applicationPrincipalName) =>
        ExecutePrincipalCommandAsync(
            administratorConnectionString,
            applicationPrincipalId,
            applicationPrincipalClientId,
            applicationPrincipalName,
            BuildCompanionReadGrants);

    /// <summary>Builds only the approved Companion read-role binding.</summary>
    internal static string BuildCompanionReadGrants(string principalAlias) => $"""
        IF DATABASE_PRINCIPAL_ID(N'{CompanionReadRuntimeRoleName}') IS NULL
            THROW 51000, 'The Companion read runtime role has not been migrated.', 1;
        IF ISNULL(IS_ROLEMEMBER(N'{CompanionReadRuntimeRoleName}', @AliasParameter), 0) <> 1
            ALTER ROLE [{CompanionReadRuntimeRoleName}] ADD MEMBER {principalAlias};
        GRANT CONNECT TO {principalAlias};
        """;

    /// <summary>Verifies the Companion identity has only its required read boundary.</summary>
    public static async Task VerifyCompanionReadPermissionsAsync(
        string administratorConnectionString,
        string? applicationPrincipalId,
        string? applicationPrincipalClientId,
        string? applicationPrincipalName)
    {
        if (!Guid.TryParse(applicationPrincipalId, out var objectId)
            || !Guid.TryParse(applicationPrincipalClientId, out _))
            throw new InvalidOperationException("The approved Companion workload identity is required.");

        const string userPrefix = "AdventuresSuiteCompanionVerifyUser_";
        const string procedurePrefix = "AdventuresSuiteCompanionVerifyProc_";
        var alias = CreatePrincipalAlias(applicationPrincipalName, objectId);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var probeUser = userPrefix + suffix;
        var probeProcedure = procedurePrefix + suffix;
        var quotedProbeUser = QuoteIdentifier(probeUser);
        var quotedProbeProcedure = QuoteIdentifier(probeProcedure);
        await using var connection = new SqlConnection(administratorConnectionString);
        await connection.OpenAsync();
        await using var administratorCommand = connection.CreateCommand();
        administratorCommand.CommandText = "SELECT USER_NAME();";
        var administratorUser = Convert.ToString(await administratorCommand.ExecuteScalarAsync())
            ?? throw new InvalidOperationException("The SQL administrator context is unavailable.");
        await AcquireCompanionVerifierLockAsync(connection);
        SqlTransaction? transaction = null;
        var impersonating = false;
        var fingerprintBefore = string.Empty;
        try
        {
            await CleanupCompanionVerifierFixturesAsync(connection, userPrefix, procedurePrefix);
            fingerprintBefore = await GetCompanionDataFingerprintAsync(connection);
            await ExecuteAdministratorAsync(
                $"CREATE USER {quotedProbeUser} WITHOUT LOGIN;", connection);
            await ExecuteAdministratorAsync(
                $"CREATE PROCEDURE dbo.{quotedProbeProcedure} AS SELECT CAST(1 AS int) AS Probe;",
                connection);

            transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await ExecuteAsync("SET XACT_ABORT OFF;", connection, transaction);
            await ExecuteAsync("""
                IF DATABASE_PRINCIPAL_ID(@Alias) IS NULL
                    THROW 51000, 'The approved Companion principal is not bound.', 1;
                IF ISNULL(IS_ROLEMEMBER(N'AdventuresSuiteCompanionReadRuntime', @Alias), 0) <> 1
                    THROW 51000, 'The approved Companion role binding is missing.', 1;
                IF ISNULL(IS_ROLEMEMBER(N'db_owner', @Alias), 0) <> 0
                    OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin', @Alias), 0) <> 0
                    OR ISNULL(IS_ROLEMEMBER(N'db_datareader', @Alias), 0) <> 0
                    OR ISNULL(IS_ROLEMEMBER(N'db_datawriter', @Alias), 0) <> 0
                    THROW 51000, 'The Companion principal has a prohibited broad role.', 1;
                """, connection, transaction, alias);

            await ExecuteAsync("EXECUTE AS USER = @Alias;", connection, transaction, probeUser);
            impersonating = true;
            if (await ScalarAsync("SELECT HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'SELECT');", connection, transaction) != 0)
                throw new InvalidOperationException("The Companion delegation baseline is invalid.");
            await ExecuteAsync("REVERT;", connection, transaction);
            impersonating = false;

            await ExecuteAsync("EXECUTE AS USER = @Alias;", connection, transaction, alias);
            impersonating = true;
            foreach (var target in CompanionReadObjects)
                await ExecuteAsync($"SELECT TOP (0) * FROM {target};", connection, transaction);

            foreach (var target in CompanionReadObjects)
            {
                await AssertAuthorizationDeniedAsync($"INSERT INTO {target} DEFAULT VALUES;", connection, transaction);
                await AssertAuthorizationDeniedAsync($"UPDATE {target} SET CreatorId = CreatorId WHERE 1 = 0;", connection, transaction);
                await AssertAuthorizationDeniedAsync($"DELETE FROM {target} WHERE 1 = 0;", connection, transaction);
            }
            await AssertAuthorizationDeniedAsync("SELECT TOP (0) * FROM auth.Users;", connection, transaction);
            await AssertAuthorizationDeniedAsync(
                $"EXECUTE dbo.{quotedProbeProcedure};", connection, transaction);
            await AssertAuthorizationDeniedAsync("SELECT TOP (0) * FROM dbo.AdventuresSuiteSchemaVersions;", connection, transaction);
            await AssertAuthorizationDeniedAsync("INSERT dbo.AdventuresSuiteSchemaVersions DEFAULT VALUES;", connection, transaction);
            await AssertAuthorizationDeniedAsync("CREATE TABLE planning.CompanionVerificationDeniedDdl (Id int);", connection, transaction);

            var controlBefore = await ScalarAsync("SELECT HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'CONTROL');", connection, transaction);
            await ExecuteAsync("GRANT CONTROL ON OBJECT::planning.AdventurePlans TO " + QuoteIdentifier(alias) + ";", connection, transaction);
            var controlAfter = await ScalarAsync("SELECT HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'CONTROL');", connection, transaction);
            if (controlBefore != 0 || controlAfter != 0)
                throw new InvalidOperationException("Companion CONTROL verification failed.");
            await AssertAuthorizationDeniedAsync(
                $"GRANT SELECT ON OBJECT::planning.AdventurePlans TO {quotedProbeUser};",
                connection, transaction);

            await ExecuteAsync("REVERT;", connection, transaction);
            impersonating = false;
            await ExecuteAsync("EXECUTE AS USER = @Alias;", connection, transaction, probeUser);
            impersonating = true;
            if (await ScalarAsync("SELECT HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'SELECT');", connection, transaction) != 0)
                throw new InvalidOperationException("Companion delegation verification failed.");
            await ExecuteAsync("REVERT;", connection, transaction);
            impersonating = false;
        }
        finally
        {
            try
            {
                try
                {
                    if (impersonating && transaction is not null)
                        await ExecuteAsync("REVERT;", connection, transaction);
                }
                finally
                {
                    if (transaction is not null && transaction.Connection is not null)
                        await transaction.RollbackAsync();
                }
            }
            finally
            {
                try
                {
                    await CleanupCompanionVerifierFixturesAsync(connection, userPrefix, procedurePrefix);
                }
                finally
                {
                    await ReleaseCompanionVerifierLockAsync(connection);
                    if (transaction is not null) await transaction.DisposeAsync();
                }
            }
        }

        await using var residue = connection.CreateCommand();
        residue.CommandText = """
            SELECT CASE WHEN NOT EXISTS (
                             SELECT 1 FROM sys.database_principals
                             WHERE LEFT(name, LEN(@UserPrefix)) = @UserPrefix)
                         AND NOT EXISTS (
                             SELECT 1 FROM sys.procedures
                             WHERE LEFT(name, LEN(@ProcedurePrefix)) = @ProcedurePrefix)
                         AND NOT EXISTS (
                             SELECT 1 FROM sys.database_permissions
                             WHERE grantee_principal_id = USER_ID(@Alias)
                               AND permission_name = N'CONTROL'
                               AND major_id = OBJECT_ID(N'planning.AdventurePlans'))
                         AND USER_NAME() = @AdministratorUser
                        THEN 0 ELSE 1 END;
            """;
        residue.Parameters.AddWithValue("@AdministratorUser", administratorUser);
        residue.Parameters.AddWithValue("@Alias", alias);
        residue.Parameters.AddWithValue("@UserPrefix", userPrefix);
        residue.Parameters.AddWithValue("@ProcedurePrefix", procedurePrefix);
        if (Convert.ToInt32(await residue.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Companion verification cleanup failed.");
        if (!string.Equals(
                fingerprintBefore,
                await GetCompanionDataFingerprintAsync(connection),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Companion verification changed application data.");
    }

    private static async Task AcquireCompanionVerifierLockAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource=N'AdventuresSuite.Companion.PermissionVerifier',
                @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=15000;
            SELECT @Result;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) < 0)
            throw new InvalidOperationException("The Companion verifier lock is unavailable.");
    }

    private static async Task ReleaseCompanionVerifierLockAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            EXEC sys.sp_releaseapplock
                @Resource=N'AdventuresSuite.Companion.PermissionVerifier', @LockOwner=N'Session';
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupCompanionVerifierFixturesAsync(
        SqlConnection connection, string userPrefix, string procedurePrefix)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF EXISTS (
                SELECT 1 FROM sys.database_principals
                WHERE LEFT(name, LEN(@UserPrefix)) = @UserPrefix
                  AND (type <> 'S' OR authentication_type <> 0))
                OR EXISTS (
                SELECT 1 FROM sys.objects AS objects
                INNER JOIN sys.schemas AS schemas ON schemas.schema_id = objects.schema_id
                WHERE LEFT(objects.name, LEN(@ProcedurePrefix)) = @ProcedurePrefix
                  AND (objects.type <> 'P' OR schemas.name <> N'dbo'))
                THROW 51000, 'Unexpected Companion verification fixtures exist.', 1;

            DECLARE @Sql nvarchar(max) = N'';
            SELECT @Sql = @Sql + N'DROP PROCEDURE dbo.' + QUOTENAME(name) + N';'
            FROM sys.procedures WHERE LEFT(name, LEN(@ProcedurePrefix)) = @ProcedurePrefix;
            EXEC sys.sp_executesql @Sql;
            SET @Sql = N'';
            SELECT @Sql = @Sql + N'DROP USER ' + QUOTENAME(name) + N';'
            FROM sys.database_principals WHERE LEFT(name, LEN(@UserPrefix)) = @UserPrefix;
            EXEC sys.sp_executesql @Sql;
            """;
        command.Parameters.AddWithValue("@UserPrefix", userPrefix);
        command.Parameters.AddWithValue("@ProcedurePrefix", procedurePrefix);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetCompanionDataFingerprintAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONCAT(
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM planning.AdventurePlans), '|',
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM planning.TravelerParticipations), '|',
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM planning.DestinationVisits), '|',
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM auth.CreatorMemberships), '|',
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM auth.CreatorMembershipRoles), '|',
                (SELECT CONCAT(COUNT_BIG(*), ':', COALESCE(CHECKSUM_AGG(BINARY_CHECKSUM(*)), 0)) FROM auth.CreatorMembershipPermissionGrants));
            """;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task ExecuteAdministratorAsync(string sql, SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static readonly string[] CompanionReadObjects =
    [
        "planning.AdventurePlans",
        "planning.TravelerParticipations",
        "planning.DestinationVisits",
        "auth.CreatorMemberships",
        "auth.CreatorMembershipRoles",
        "auth.CreatorMembershipPermissionGrants"
    ];

    private static async Task AssertAuthorizationDeniedAsync(
        string sql, SqlConnection connection, SqlTransaction transaction)
    {
        try
        {
            await ExecuteAsync(sql, connection, transaction);
        }
        catch (SqlException exception) when (exception.Number is 229 or 262 or 15151)
        {
            return;
        }
        throw new InvalidOperationException("A prohibited Companion SQL operation was not denied by authorization.");
    }

    private static async Task ExecuteAsync(
        string sql, SqlConnection connection, SqlTransaction transaction, string? alias = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        if (alias is not null) command.Parameters.AddWithValue("@Alias", alias);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(
        string sql, SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

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
                OR OBJECT_ID(N'auth.CreatorMemberships', N'U') IS NULL
                OR OBJECT_ID(N'auth.CreatorMembershipRoles', N'U') IS NULL
                OR OBJECT_ID(N'auth.CreatorMembershipPermissionGrants', N'U') IS NULL
                OR OBJECT_ID(N'audit.AuditEvents', N'U') IS NULL
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
