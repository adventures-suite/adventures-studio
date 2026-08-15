using Azure.Core;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Creates SQL connections for one explicitly selected migration credential mode.</summary>
internal sealed class MigrationSqlConnectionFactory
{
    private readonly string connectionString;
    private readonly string? accessToken;

    private MigrationSqlConnectionFactory(string connectionString, string? accessToken)
    {
        this.connectionString = connectionString;
        this.accessToken = accessToken;
    }

    /// <summary>Creates a factory without permitting credential-mode fallback.</summary>
    internal static MigrationSqlConnectionFactory Create(
        string connectionString,
        MigrationCredentialMode credentialMode,
        AccessToken token) => credentialMode switch
        {
            MigrationCredentialMode.GitHubOidcAzureCli when !string.IsNullOrWhiteSpace(token.Token) =>
                new(connectionString, token.Token),
            MigrationCredentialMode.AzureManagedIdentity => new(connectionString, null),
            MigrationCredentialMode.GitHubOidcAzureCli =>
                throw new InvalidOperationException("The reviewed Azure CLI SQL token is missing."),
            _ => throw new InvalidOperationException("The migration credential mode is not approved.")
        };

    /// <summary>Creates a connection carrying only the credential selected for this operation.</summary>
    internal SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(connectionString);
        if (accessToken is not null)
        {
            connection.AccessToken = accessToken;
        }

        return connection;
    }
}
