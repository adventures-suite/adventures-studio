using Microsoft.Extensions.Configuration;
using TheSimontonAdventures.Web.Planning;

namespace TheSimontonAdventures.Web.Tests;

/// <summary>Verifies the fail-closed activation boundary for preview Planner catalogs.</summary>
public sealed class PlannerPreviewCatalogActivationTests
{
    /// <summary>Local Development authentication retains its isolated catalog behavior.</summary>
    [Fact]
    public void IsEnabled_LocalDevelopmentAuthentication_ReturnsTrue()
    {
        var configuration = Configuration();

        var enabled = PlannerPreviewCatalogActivation.IsEnabled(configuration, "Development");

        Assert.True(enabled);
    }

    /// <summary>An external provider requires both exact hosted-development settings.</summary>
    [Fact]
    public void IsEnabled_ExactHostedDevelopmentPreview_ReturnsTrue()
    {
        var configuration = Configuration(
            ("PlannerCatalog:Mode", PlannerPreviewCatalogActivation.DevelopmentPreviewMode),
            ("Deployment:Environment", PlannerPreviewCatalogActivation.DevelopmentDeploymentEnvironment));

        var enabled = PlannerPreviewCatalogActivation.IsEnabled(configuration, "ExternalProvider");

        Assert.True(enabled);
    }

    /// <summary>Missing, partial, production, disabled, and case-drifted settings fail closed.</summary>
    [Theory]
    [InlineData("ExternalProvider", null, null)]
    [InlineData("ExternalProvider", "DevelopmentPreview", null)]
    [InlineData("ExternalProvider", null, "Development")]
    [InlineData("ExternalProvider", "DevelopmentPreview", "Production")]
    [InlineData("ExternalProvider", "developmentpreview", "Development")]
    [InlineData("ExternalProvider", "DevelopmentPreview", "development")]
    [InlineData("Disabled", "DevelopmentPreview", "Development")]
    [InlineData(null, "DevelopmentPreview", "Development")]
    public void IsEnabled_AnythingOtherThanExactApprovedBoundary_ReturnsFalse(
        string? authenticationMode,
        string? catalogMode,
        string? deploymentEnvironment)
    {
        var configuration = Configuration(
            ("PlannerCatalog:Mode", catalogMode),
            ("Deployment:Environment", deploymentEnvironment));

        var enabled = PlannerPreviewCatalogActivation.IsEnabled(configuration, authenticationMode);

        Assert.False(enabled);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => item.Value))
            .Build();
}
