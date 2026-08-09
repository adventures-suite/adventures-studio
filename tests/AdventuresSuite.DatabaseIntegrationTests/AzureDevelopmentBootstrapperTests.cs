using AdventuresSuite.DatabaseMigrator;

namespace AdventuresSuite.DatabaseIntegrationTests;

public sealed class AzureDevelopmentBootstrapperTests
{
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
