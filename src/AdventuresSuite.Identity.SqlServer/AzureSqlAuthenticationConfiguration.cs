using Microsoft.Data.SqlClient;

namespace AdventuresSuite.Identity.SqlServer;

/// <summary>Validates the exact Azure SQL target and workload-authentication boundary.</summary>
public static class AzureSqlAuthenticationConfiguration
{
    /// <summary>Parses and returns a canonical, approved Managed Identity connection string.</summary>
    public static string Validate(
        string connectionString,
        string expectedServerName,
        string expectedDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString)
            || string.IsNullOrWhiteSpace(expectedServerName)
            || string.IsNullOrWhiteSpace(expectedDatabaseName)
            || expectedServerName != expectedServerName.Trim()
            || expectedDatabaseName != expectedDatabaseName.Trim())
        {
            throw new InvalidOperationException("The approved Azure SQL target is required.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("The Azure SQL connection configuration is invalid.", exception);
        }

        var expectedDataSource = $"tcp:{expectedServerName},1433";
        if (!string.Equals(builder.DataSource, expectedDataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(builder.InitialCatalog, expectedDatabaseName, StringComparison.Ordinal)
            || builder.Authentication != SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
            || (!builder.Encrypt.Equals(SqlConnectionEncryptOption.Mandatory)
                && !builder.Encrypt.Equals(SqlConnectionEncryptOption.Strict))
            || builder.TrustServerCertificate
            || builder.IntegratedSecurity
            || !string.IsNullOrEmpty(builder.UserID)
            || !string.IsNullOrEmpty(builder.Password)
            || !string.IsNullOrEmpty(builder.AttachDBFilename))
        {
            throw new InvalidOperationException("The Azure SQL connection must use the approved encrypted Managed Identity target.");
        }

        return builder.ConnectionString;
    }
}

/// <summary>Proves that the runtime identity can read the required authentication schema.</summary>
public sealed class SqlAuthenticationReadinessProbe(string connectionString)
{
    private readonly string connectionString = string.IsNullOrWhiteSpace(connectionString)
        ? throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString))
        : connectionString;

    /// <summary>Executes a bounded, read-only schema and permission probe.</summary>
    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET NOCOUNT ON;
            IF OBJECT_ID(N'auth.Users', N'U') IS NULL
                OR OBJECT_ID(N'auth.ExternalIdentities', N'U') IS NULL
                OR OBJECT_ID(N'auth.UserSessions', N'U') IS NULL
                OR COL_LENGTH(N'auth.UserSessions', N'ExternalIdentityId') IS NULL
                THROW 51000, 'Authentication persistence is unavailable.', 1;

            SELECT TOP (0) UserId, Status, SecurityVersion FROM auth.Users;
            SELECT TOP (0) ExternalIdentityId, UserId, Provider, Issuer, Subject
                FROM auth.ExternalIdentities;
            SELECT TOP (0) UserSessionId, UserId, ExternalIdentityId, SecurityVersion
                FROM auth.UserSessions;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
