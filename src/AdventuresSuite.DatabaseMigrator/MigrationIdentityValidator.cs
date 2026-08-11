using System.Text.Json;
using Azure.Core;
using Microsoft.Data.SqlClient;

namespace AdventuresSuite.DatabaseMigrator;

/// <summary>Validates the exact migration workload token and contained SQL principal.</summary>
internal static class MigrationIdentityValidator
{
    internal static MigrationWorkloadIdentityEvidence ValidateWorkloadToken(
        AccessToken token,
        Guid expectedTenantId,
        Guid expectedObjectId,
        Guid expectedClientId,
        string expectedAudience)
    {
        var claims = ReadClaims(token.Token);
        RequireClaim(claims, "tid", expectedTenantId.ToString());
        RequireClaim(claims, "oid", expectedObjectId.ToString());
        RequireClaim(claims, "aud", expectedAudience);
        var clientClaim = claims.TryGetValue("appid", out var appId) ? appId
            : claims.TryGetValue("azp", out var authorizedParty) ? authorizedParty : null;
        if (!string.Equals(clientClaim, expectedClientId.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The migration token client identity is not approved.");

        return new(expectedTenantId, expectedObjectId, expectedClientId, expectedAudience);
    }

    internal static async Task<MigrationIdentityEvidence> ValidateAsync(
        AccessToken token,
        string connectionString,
        Guid expectedTenantId,
        Guid expectedObjectId,
        Guid expectedClientId,
        string expectedPrincipalName,
        string expectedServer,
        string expectedDatabase)
    {
        _ = ValidateWorkloadToken(token, expectedTenantId, expectedObjectId, expectedClientId,
            "https://database.windows.net/");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.Authentication != SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
            || !string.Equals(builder.UserID, expectedClientId.ToString(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(builder.InitialCatalog, expectedDatabase, StringComparison.Ordinal)
            || !ServerMatches(builder.DataSource, expectedServer))
            throw new InvalidOperationException("The migration connection target or identity is not approved.");

        var alias = AzureDevelopmentBootstrapper.CreatePrincipalAlias(expectedPrincipalName, expectedObjectId);
        var tokenBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            Authentication = SqlAuthenticationMethod.NotSpecified,
            UserID = string.Empty
        };
        await using var connection = new SqlConnection(tokenBuilder.ConnectionString)
        {
            AccessToken = token.Token
        };
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CAST(SERVERPROPERTY(N'ServerName') AS nvarchar(256)), DB_NAME(), USER_NAME(),
                   CASE WHEN EXISTS (
                       SELECT 1 FROM sys.database_principals
                       WHERE name = @Alias AND type = N'E'
                         AND CAST(sid AS uniqueidentifier) = @ClientId)
                   THEN 1 ELSE 0 END;
            """;
        command.Parameters.AddWithValue("@Alias", alias);
        command.Parameters.AddWithValue("@ClientId", expectedClientId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()
            || !string.Equals(reader.GetString(0), expectedServer, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(reader.GetString(1), expectedDatabase, StringComparison.Ordinal)
            || !string.Equals(reader.GetString(2), alias, StringComparison.Ordinal)
            || reader.GetInt32(3) != 1)
            throw new InvalidOperationException("SQL did not confirm the approved migration principal and target.");

        return new(expectedTenantId, expectedObjectId, expectedClientId, alias, expectedServer, expectedDatabase);
    }

    internal static IReadOnlyDictionary<string, string> ReadClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) throw new InvalidOperationException("The migration token is not a JWT.");
        var bytes = Base64UrlDecode(parts[1]);
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
    }

    private static void RequireClaim(
        IReadOnlyDictionary<string, string> claims, string name, string expected)
    {
        if (!claims.TryGetValue(name, out var actual)
            || !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The migration token {name} claim is not approved.");
    }

    private static bool ServerMatches(string dataSource, string expectedServer)
    {
        var normalized = dataSource.Trim();
        if (normalized.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];
        normalized = normalized.Split(',')[0];
        return normalized.Equals(
                   expectedServer + ".database.windows.net", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(expectedServer, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Convert.FromBase64String(padded);
    }
}

internal sealed record MigrationWorkloadIdentityEvidence(
    Guid TenantId,
    Guid ObjectId,
    Guid ClientId,
    string Audience);

internal sealed record MigrationIdentityEvidence(
    Guid TenantId,
    Guid ObjectId,
    Guid ClientId,
    string SqlPrincipalAlias,
    string Server,
    string Database);
