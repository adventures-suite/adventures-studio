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

/// <summary>Runs the bounded Companion SQL readiness check.</summary>
public interface ICompanionSqlReadinessProbe
{
    /// <summary>Returns whether the exact least-privilege read boundary is ready.</summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}

/// <summary>Bounded readiness failure categories safe for telemetry.</summary>
public enum CompanionSqlReadinessFailureCategory
{
    /// <summary>The SQL network or service connection failed.</summary>
    Connection,
    /// <summary>Managed Identity authentication failed.</summary>
    Authentication,
    /// <summary>The SQL operation timed out.</summary>
    Timeout,
    /// <summary>The permission projection probe failed.</summary>
    Probe
}

/// <summary>Represents a sanitized SQL readiness failure.</summary>
public sealed class CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory category) : Exception
{
    /// <summary>Gets the bounded failure category.</summary>
    public CompanionSqlReadinessFailureCategory Category { get; } = category;
}

/// <summary>Performs a read-only readiness and least-privilege probe.</summary>
public sealed class CompanionSqlReadinessProbe(string connectionString) : ICompanionSqlReadinessProbe
{
    /// <summary>Verifies connectivity, required reads, and absence of mutation or DDL permissions.</summary>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException exception) when (exception.Number == -2)
        {
            throw new CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory.Timeout);
        }
        catch (SqlException exception) when (exception.Number is 18456 or 33134)
        {
            throw new CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory.Authentication);
        }
        catch (SqlException)
        {
            throw new CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory.Connection);
        }
        catch (TimeoutException)
        {
            throw new CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory.Timeout);
        }
        catch (Exception)
        {
            throw new CompanionSqlReadinessException(CompanionSqlReadinessFailureCategory.Probe);
        }
    }
}
