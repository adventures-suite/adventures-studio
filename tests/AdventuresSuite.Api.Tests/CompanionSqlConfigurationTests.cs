using AdventuresSuite.Companion.SqlServer;

namespace AdventuresSuite.Api.Tests;

/// <summary>Verifies fail-fast Companion SQL configuration.</summary>
public sealed class CompanionSqlConfigurationTests
{
    private const string ClientId = "11111111-2222-3333-4444-555555555555";

    /// <summary>Accepts only the exact encrypted Managed Identity target.</summary>
    [Fact]
    public void Validate_AcceptsExactManagedIdentityTarget()
    {
        var value = CompanionSqlConfiguration.Validate(
            $"Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDevelopment;Authentication=Active Directory Managed Identity;User ID={ClientId};Encrypt=True;TrustServerCertificate=False",
            "tcp:adventures-suite-dev-sql.database.windows.net,1433",
            "AdventuresSuiteDevelopment",
            ClientId);

        Assert.DoesNotContain("Password", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active Directory Managed Identity", value, StringComparison.Ordinal);
    }

    /// <summary>Rejects secrets, wrong targets, trust bypasses, and wrong identities.</summary>
    [Theory]
    [InlineData("Password=secret")]
    [InlineData("TrustServerCertificate=True")]
    [InlineData("Database=other")]
    [InlineData("User ID=aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    public void Validate_RejectsBoundaryMismatch(string replacement)
    {
        var baseline = $"Server=tcp:adventures-suite-dev-sql.database.windows.net,1433;Database=AdventuresSuiteDevelopment;Authentication=Active Directory Managed Identity;User ID={ClientId};Encrypt=True;TrustServerCertificate=False";
        var key = replacement[..replacement.IndexOf('=')];
        var parts = baseline.Split(';').Where(value => !value.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase));
        var candidate = $"{string.Join(';', parts)};{replacement}";

        Assert.Throws<InvalidOperationException>(() => CompanionSqlConfiguration.Validate(
            candidate,
            "tcp:adventures-suite-dev-sql.database.windows.net,1433",
            "AdventuresSuiteDevelopment",
            ClientId));
    }
}
