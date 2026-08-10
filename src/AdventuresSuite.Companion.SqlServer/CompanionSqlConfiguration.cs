using Microsoft.Data.SqlClient;

namespace AdventuresSuite.Companion.SqlServer;

/// <summary>Validates the exact encrypted Managed Identity SQL read boundary.</summary>
public static class CompanionSqlConfiguration
{
    /// <summary>Validates and canonicalizes an approved Azure SQL connection string.</summary>
    public static string Validate(
        string? connectionString, string? approvedServer, string? approvedDatabase,
        string? managedIdentityClientId)
    {
        if (string.IsNullOrWhiteSpace(connectionString)
            || string.IsNullOrWhiteSpace(approvedServer)
            || string.IsNullOrWhiteSpace(approvedDatabase)
            || !Guid.TryParse(managedIdentityClientId, out var clientId))
            throw new InvalidOperationException("Complete Companion SQL Managed Identity configuration is required.");

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("The Companion SQL connection configuration is invalid.", exception);
        }

        if (!string.Equals(builder.DataSource, approvedServer, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(builder.InitialCatalog, approvedDatabase, StringComparison.Ordinal)
            || builder.Authentication != SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
            || !string.Equals(builder.UserID, clientId.ToString(), StringComparison.OrdinalIgnoreCase)
            || builder.Password.Length != 0
            || (!builder.Encrypt.Equals(SqlConnectionEncryptOption.Mandatory)
                && !builder.Encrypt.Equals(SqlConnectionEncryptOption.Strict))
            || builder.TrustServerCertificate
            || builder.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                "Companion SQL must use the exact encrypted database and approved Managed Identity.");
        }

        builder.PersistSecurityInfo = false;
        builder.Pooling = true;
        return builder.ConnectionString;
    }
}

/// <summary>Performs a read-only readiness and least-privilege probe.</summary>
public sealed class CompanionSqlReadinessProbe(string connectionString)
{
    /// <summary>Verifies connectivity, required reads, and absence of mutation or DDL permissions.</summary>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN
                HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'planning.TravelerParticipations', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'planning.DestinationVisits', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'auth.CreatorMemberships', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'auth.CreatorMembershipRoles', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'auth.CreatorMembershipPermissionGrants', N'OBJECT', N'SELECT') = 1
                AND HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'INSERT') = 0
                AND HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'UPDATE') = 0
                AND HAS_PERMS_BY_NAME(N'planning.AdventurePlans', N'OBJECT', N'DELETE') = 0
                AND HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'ALTER') = 0
                AND IS_ROLEMEMBER(N'db_ddladmin') = 0
                AND IS_ROLEMEMBER(N'db_datawriter') = 0
                AND IS_ROLEMEMBER(N'db_owner') = 0
                THEN 1 ELSE 0 END;
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }
}
