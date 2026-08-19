using Microsoft.Data.SqlClient;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the bounded local Alpha bootstrap safety boundary.</summary>
public sealed class LocalAlphaBootstrapTests
{
    private const string Approved = "Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=local-test-only;Encrypt=True;TrustServerCertificate=True";

    /// <summary>Ensures only the explicit local disposable target is accepted.</summary>
    [Fact]
    public void ValidateTarget_ApprovedLocalApplicationIdentity_Passes() =>
        LocalAlphaBootstrap.ValidateTarget(Approved, "Development", enabled: true);

    /// <summary>Ensures production, missing enablement, Azure, shared databases, and administrator identities fail.</summary>
    [Theory]
    [InlineData("Production", true, "Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Development", false, "Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Development", true, "Server=tcp:shared.database.windows.net,1433;Database=AdventuresSuiteLocalAlpha;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Development", true, "Server=localhost,1433;Database=SharedPlanner;User ID=adventures_alpha_app;Password=x;Encrypt=True;TrustServerCertificate=True")]
    [InlineData("Development", true, "Server=localhost,1433;Database=AdventuresSuiteLocalAlpha;User ID=sa;Password=x;Encrypt=True;TrustServerCertificate=True")]
    public void ValidateTarget_UnapprovedBoundary_Throws(string environment, bool enabled, string connectionString) =>
        Assert.Throws<InvalidOperationException>(() =>
            LocalAlphaBootstrap.ValidateTarget(connectionString, environment, enabled));

    /// <summary>Ensures the bootstrap's fixed identities cannot be supplied by any client input.</summary>
    [Fact]
    public void FixedIdentity_IsCompiledServerConfiguration()
    {
        Assert.Equal("creator_local_alpha", LocalAlphaBootstrap.CreatorId);
        Assert.Equal("user_local_alpha_planner", LocalAlphaBootstrap.UserId);
        Assert.Equal("Planner", LocalAlphaBootstrap.CreatorRole);
    }
}
