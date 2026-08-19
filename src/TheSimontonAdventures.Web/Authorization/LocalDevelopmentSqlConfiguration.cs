using Microsoft.Data.SqlClient;

namespace TheSimontonAdventures.Web.Authorization;

/// <summary>Validates the isolated local SQL target used by explicit Development authentication.</summary>
public static class LocalDevelopmentSqlConfiguration
{
    /// <summary>Returns a normalized local connection only for the explicitly enabled Development environment.</summary>
    public static string Validate(
        string connectionString,
        string approvedDatabaseName,
        string environmentName,
        bool explicitlyEnabled)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.Ordinal)
            || !explicitlyEnabled
            || string.IsNullOrWhiteSpace(approvedDatabaseName))
        {
            throw new InvalidOperationException("Local SQL is available only to explicitly enabled Development authentication.");
        }

        var value = new SqlConnectionStringBuilder(connectionString);
        var host = value.DataSource.Split(',', 2)[0].Replace("tcp:", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (host is not ("localhost" or "127.0.0.1" or "::1")
            || !string.Equals(value.InitialCatalog, approvedDatabaseName, StringComparison.Ordinal)
            || !approvedDatabaseName.StartsWith("AdventuresSuiteLocal", StringComparison.Ordinal)
            || value.IntegratedSecurity
            || string.IsNullOrWhiteSpace(value.UserID)
            || string.IsNullOrWhiteSpace(value.Password)
            || value.Authentication != SqlAuthenticationMethod.NotSpecified
            || value.Encrypt != SqlConnectionEncryptOption.Mandatory
            || !value.TrustServerCertificate)
        {
            throw new InvalidOperationException("The development SQL target must be an approved encrypted localhost database with explicit application credentials.");
        }

        return value.ConnectionString;
    }
}
