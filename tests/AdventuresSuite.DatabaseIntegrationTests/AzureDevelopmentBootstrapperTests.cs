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
    public void MigrationGrantsCreateAdministratorPrerequisitesAndExactTemporaryCatalog()
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
        Assert.Contains("CREATE ROLE [AdventuresSuiteCompanionReadRuntime] AUTHORIZATION [dbo];", grants, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE [AdventuresSuitePlanningRuntime] AUTHORIZATION [dbo];", grants, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.AdventuresSuiteSchemaVersions", grants, StringComparison.Ordinal);
        Assert.Contains(
            "ALTER USER [migration-principal] WITH DEFAULT_SCHEMA = [dbo];",
            grants,
            StringComparison.Ordinal);
        Assert.Contains("GRANT CREATE TABLE", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT VIEW DEFINITION", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT CONTROL ON SCHEMA::planning", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT CONTROL ON SCHEMA::auth", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT CONTROL ON SCHEMA::audit", grants, StringComparison.Ordinal);
        Assert.Contains("GRANT SELECT, INSERT ON OBJECT::dbo.AdventuresSuiteSchemaVersions", grants, StringComparison.Ordinal);
        Assert.DoesNotContain("ADD MEMBER [migration-principal]", grants, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapUsesExactSidWithoutDirectoryLookup()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src/AdventuresSuite.DatabaseMigrator/AzureDevelopmentBootstrapper.cs"));

        Assert.Contains("CREATE USER {quotedAlias} WITH SID = ", source, StringComparison.Ordinal);
        Assert.Contains("TYPE = E", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM EXTERNAL PROVIDER", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH OBJECT_ID", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures runtime binding includes the dedicated Planning role without broad roles.</summary>
    [Fact]
    public void RuntimeBinding_UsesDedicatedPlanningRoleWithoutBroadDatabaseRoles()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src/AdventuresSuite.DatabaseMigrator/SqlAdministratorOperationRunner.cs"));

        Assert.Contains("AdventuresSuitePlanningRuntime", source, StringComparison.Ordinal);
        Assert.Contains("approved application database principal does not exist", source, StringComparison.Ordinal);
        Assert.Contains("prohibited broad role authority", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER ROLE [db_datareader] ADD MEMBER", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ROLE [db_datawriter] ADD MEMBER", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ROLE [db_ddladmin] ADD MEMBER", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanupRevokesOnlyTemporaryCatalogDropsUserAndRetainsPrerequisites()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src/AdventuresSuite.DatabaseMigrator/AzureDevelopmentBootstrapper.cs"));

        foreach (var expected in new[]
        {
            "REVOKE CONNECT", "REVOKE CREATE TABLE", "REVOKE VIEW DEFINITION",
            "REVOKE CONTROL ON SCHEMA::planning", "REVOKE CONTROL ON SCHEMA::auth",
            "REVOKE CONTROL ON SCHEMA::audit", "REVOKE SELECT, INSERT, UPDATE, DELETE",
            "DROP USER"
        }) Assert.Contains(expected, source, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP SCHEMA", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ROLE", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", source, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TheSimontonAdventures.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
