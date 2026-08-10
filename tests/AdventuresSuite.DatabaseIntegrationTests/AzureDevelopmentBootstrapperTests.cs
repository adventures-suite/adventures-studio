using AdventuresSuite.DatabaseMigrator;

namespace AdventuresSuite.DatabaseIntegrationTests;

public sealed class AzureDevelopmentBootstrapperTests
{
    /// <summary>Ensures Companion binding cannot inherit broad or write roles.</summary>
    [Fact]
    public void CompanionReadBinding_ContainsOnlyDedicatedRoleAndConnect()
    {
        var grants = AzureDevelopmentBootstrapper.BuildCompanionReadGrants("[companion-principal]");

        Assert.Contains("AdventuresSuiteCompanionReadRuntime", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT CONNECT", grants, StringComparison.Ordinal);
        Assert.DoesNotContain("db_datareader", grants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_datawriter", grants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("db_ddladmin", grants, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdventuresSuiteAuthenticationRuntime", grants, StringComparison.Ordinal);
        Assert.DoesNotContain("AdventuresSuiteMembershipRuntime", grants, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationGrantsAssignOwnershipCapableDefaultSchema()
    {
        var grants = AzureDevelopmentBootstrapper.BuildMigrationGrants("[migration-principal]");

        Assert.Contains(
            "CREATE ROLE [AdventuresSuiteAuthenticationRuntime] AUTHORIZATION [dbo];",
            grants,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE ROLE [AdventuresSuiteMembershipRuntime] AUTHORIZATION [dbo];",
            grants,
            StringComparison.Ordinal);
        Assert.Contains(
            "The membership runtime principal name is not an approved database role.",
            grants,
            StringComparison.Ordinal);
        Assert.Contains(
            "ALTER USER [migration-principal] WITH DEFAULT_SCHEMA = [dbo];",
            grants,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePrincipalAliasUsesExactDisplayNameAndObjectIdSuffix()
    {
        var alias = AzureDevelopmentBootstrapper.CreatePrincipalAlias(
            "adventures-suite-migrate-dev",
            Guid.Parse("ce76a652-2741-4324-8a1c-18f25409dee0"));

        Assert.Equal("adventures-suite-migrate-dev-ce76a", alias);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" migration-principal")]
    [InlineData("migration-principal ")]
    [InlineData("migration\nprincipal")]
    [InlineData("migration principal")]
    [InlineData("migration'principal")]
    [InlineData("migration]principal")]
    public void CreatePrincipalAliasRejectsUnapprovedDisplayNames(string? displayName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            AzureDevelopmentBootstrapper.CreatePrincipalAlias(displayName, Guid.NewGuid()));
    }

    [Fact]
    public void CreatePrincipalAliasRejectsValuesBeyondSqlSysnameLimit()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AzureDevelopmentBootstrapper.CreatePrincipalAlias(new string('a', 123), Guid.NewGuid()));
    }
}
